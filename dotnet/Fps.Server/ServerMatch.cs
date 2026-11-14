using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 試合の枠だけを持つ。点数・勝ち負け・時間はルールのモジュールが決めて <see cref="SetScores"/> と <see cref="End"/> で知らせる。
    /// 枠と決め方を分けるのは、ルールのモジュールを差し替えても出入り・チーム分け・JSON のイベントが変わらないようにするため。
    /// </summary>
    internal sealed class ServerMatch
    {
        public const float DefaultRestartDelaySeconds = 10f;

        // playerId の昇順に回して、同じ入力なら同じ順に知らせる
        private readonly SortedDictionary<int, ServerPlayer> _players = new SortedDictionary<int, ServerPlayer>();
        private readonly Dictionary<ConnectionId, ServerPlayer> _byConnection = new Dictionary<ConnectionId, ServerPlayer>();
        private readonly TextWriter _log;
        private readonly ServerEventWriter _events;
        private readonly Func<DateTime> _utcNow;
        private float _restartRemaining;

        public ServerMatch(float restartDelaySeconds, TextWriter log, ServerEventWriter events, Func<DateTime> utcNow)
        {
            if (!(restartDelaySeconds >= 0f) || float.IsInfinity(restartDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(restartDelaySeconds), restartDelaySeconds, "restartDelaySeconds must not be negative and must be finite");
            }

            RestartDelaySeconds = restartDelaySeconds;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            MatchId = ServerEventLog.NewMatchId();
        }

        public event Action<ServerPlayer> PlayerJoined;

        public event Action<ServerPlayer> PlayerLeft;

        public event Action Started;

        public event Action<Team?> Ended;

        public event Action Restarted;

        public float RestartDelaySeconds { get; }

        public string MatchId { get; private set; }

        public GameModeState State { get; private set; }

        public int PlayerCount => _players.Count;

        public int AlphaScore { get; private set; }

        public int BravoScore { get; private set; }

        public float RestartRemainingSeconds => State == GameModeState.Ended ? Math.Max(0f, _restartRemaining) : 0f;

        public IEnumerable<ServerPlayer> Players => _players.Values;

        /// <summary>
        /// Bot の Id と重ならない範囲に収めるのは、成績の行や JSON の playerId で Bot と見分けられなくなるため。
        /// </summary>
        public static bool TryGetPlayerId(ConnectionId connection, out int playerId)
        {
            if (connection.IsValid && connection.Value < BotRoster.FirstBotPlayerId)
            {
                playerId = (int)connection.Value;
                return true;
            }

            playerId = 0;
            return false;
        }

        public bool TryGetPlayer(ConnectionId connection, out ServerPlayer player)
        {
            return _byConnection.TryGetValue(connection, out player);
        }

        public bool TryGetPlayer(int playerId, out ServerPlayer player)
        {
            return _players.TryGetValue(playerId, out player);
        }

        public int CountPlayers(Team team)
        {
            var count = 0;
            foreach (var player in _players.Values)
            {
                if (player.Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        public ServerPlayer Join(ConnectionId connection, string rejoinKey)
        {
            if (!TryGetPlayerId(connection, out var playerId))
            {
                throw new ArgumentException("connection " + connection + " does not fit a player id", nameof(connection));
            }

            if (_byConnection.ContainsKey(connection))
            {
                throw new ArgumentException("connection " + connection + " has already joined", nameof(connection));
            }

            var team = TeamAssigner.Choose(CountPlayers(Team.Alpha), CountPlayers(Team.Bravo), AlphaScore, BravoScore);
            var player = new ServerPlayer(connection, playerId, team, rejoinKey);
            _players.Add(playerId, player);
            _byConnection.Add(connection, player);

            _log.WriteLine(MatchLog.Joined(playerId, team));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.PlayerJoined(_utcNow(), MatchId, playerId, null, team, false));
            }

            PlayerJoined?.Invoke(player);
            TryStart();
            return player;
        }

        public bool Leave(ConnectionId connection)
        {
            if (!_byConnection.TryGetValue(connection, out var player))
            {
                return false;
            }

            _byConnection.Remove(connection);
            _players.Remove(player.PlayerId);
            _log.WriteLine(MatchLog.Left(player.PlayerId));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.PlayerLeft(_utcNow(), MatchId, player.PlayerId, null));
            }

            PlayerLeft?.Invoke(player);
            return true;
        }

        public void SetScores(int alphaScore, int bravoScore)
        {
            if (alphaScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaScore), alphaScore, "alphaScore must not be negative");
            }

            if (bravoScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoScore), bravoScore, "bravoScore must not be negative");
            }

            AlphaScore = alphaScore;
            BravoScore = bravoScore;
        }

        public bool End(Team? winner)
        {
            if (winner.HasValue && !Enum.IsDefined(winner.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(winner), winner, "winner must be a Team or null");
            }

            if (State != GameModeState.Playing)
            {
                return false;
            }

            State = GameModeState.Ended;
            _restartRemaining = RestartDelaySeconds;
            _log.WriteLine(MatchLog.Ended(winner));
            _log.WriteLine(MatchLog.NextMatch(RestartDelaySeconds));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.MatchEnded(_utcNow(), MatchId, winner, AlphaScore, BravoScore));
            }

            Ended?.Invoke(winner);
            return true;
        }

        /// <summary>終わった回の Tick の残りを待ちに数えないのは、Core の TeamDeathmatchSession と同じ間で次の試合を始めるため。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (State != GameModeState.Ended)
            {
                return;
            }

            _restartRemaining -= deltaSeconds;
            if (_restartRemaining <= 0f)
            {
                Restart();
            }
        }

        private void Restart()
        {
            State = GameModeState.Waiting;
            _restartRemaining = 0f;
            AlphaScore = 0;
            BravoScore = 0;
            MatchId = ServerEventLog.NewMatchId();
            _log.WriteLine(MatchLog.Restart());
            Restarted?.Invoke();
            TryStart();
        }

        private void TryStart()
        {
            if (State != GameModeState.Waiting || CountPlayers(Team.Alpha) == 0 || CountPlayers(Team.Bravo) == 0)
            {
                return;
            }

            State = GameModeState.Playing;
            _log.WriteLine(MatchLog.Started(PlayerCount));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.MatchStarted(_utcNow(), MatchId, PlayerCount));
            }

            Started?.Invoke();
        }
    }
}
