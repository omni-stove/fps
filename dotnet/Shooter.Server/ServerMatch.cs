using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 点数・勝ち負け・時間はルールのモジュールに決めさせ、ここは試合の枠だけを持つ。
    // 枠と決め方を分けるのは、ルールのモジュールを差し替えても出入り・チーム分け・JSON のイベントが変わらないようにするため。
    internal sealed partial class ServerMatch
    {
        public const float DefaultRestartDelaySeconds = 10f;

        // playerId の昇順に回して、同じ入力なら同じ順に知らせる
        private readonly SortedDictionary<int, ServerPlayer> _players = new SortedDictionary<int, ServerPlayer>();
        private readonly Dictionary<ConnectionId, ServerPlayer> _byConnection = new Dictionary<ConnectionId, ServerPlayer>();
        private readonly TextWriter _log;
        private readonly ServerEventWriter _events;
        private readonly Func<DateTime> _utcNow;
        private readonly float _restartDelaySeconds;
        private float _restartRemaining;
        private int _botCount;
        private int _maxPlayers = NetMatchSettings.DefaultMaxPlayers;
        private long _joinCount;

        public ServerMatch(
            float restartDelaySeconds,
            TextWriter log,
            ServerEventWriter events,
            Func<DateTime> utcNow,
            int maxTeamSizeDifference = NetMatchSettings.DefaultMaxTeamSizeDifference,
            float mapVoteSeconds = NetMatchSettings.DefaultMapVoteSeconds)
        {
            if (!(restartDelaySeconds >= 0f) || float.IsInfinity(restartDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(restartDelaySeconds), restartDelaySeconds, "restartDelaySeconds must not be negative and must be finite");
            }

            if (maxTeamSizeDifference < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTeamSizeDifference), maxTeamSizeDifference, "maxTeamSizeDifference must not be negative");
            }

            if (!(mapVoteSeconds >= 0f) || float.IsInfinity(mapVoteSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mapVoteSeconds), mapVoteSeconds, "mapVoteSeconds must not be negative and must be finite");
            }

            _restartDelaySeconds = restartDelaySeconds;
            MapVoteSeconds = mapVoteSeconds;
            MaxTeamSizeDifference = maxTeamSizeDifference;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            MatchId = ServerEventLog.NewMatchId();
        }

        public delegate Team TeamChooser(string rejoinKey, out bool rejoined);

        public event Action<ServerPlayer> PlayerJoined;

        public event Action<ServerPlayer> PlayerLeft;

        public event Action Started;

        public event Action<Team?> Ended;

        public event Action Restarted;

        // マップの投票をするときに MapVoteSeconds より短くしないのは、締め切る前に次の試合が始まらないようにするため。
        public float RestartDelaySeconds => HoldsMapVote ? Math.Max(_restartDelaySeconds, MapVoteSeconds) : _restartDelaySeconds;

        public int MaxTeamSizeDifference { get; }

        public string MatchId { get; private set; }

        public GameModeState State { get; private set; }

        public int PlayerCount => _players.Count;

        // 抜けるのを待つ (drain) ・ロビーに載せる人数に Bot を数えると、人がいなくても試合が続いているように見えるため
        public int HumanCount => _players.Count - _botCount;

        public int BotCount => _botCount;

        public int AlphaScore { get; private set; }

        public int BravoScore { get; private set; }

        public float RestartRemainingSeconds => State == GameModeState.Ended ? Math.Max(0f, _restartRemaining) : 0f;

        public IEnumerable<ServerPlayer> Players => _players.Values;

        // ルールのモジュールが、切断した人を同じチームに戻すために差し替えられるようにする。
        public TeamChooser ChooseTeam { get; set; }

        // その半分 (TeamAssigner.TeamCapacity) に届いたチームを満員と見るのは、チームごとの定員を別の設定に増やさないため。
        public int MaxPlayers
        {
            get => _maxPlayers;
            set
            {
                if (value < NetMatchSettings.MinMaxPlayers || value > NetMatchSettings.MaxMaxPlayers)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "MaxPlayers must be between " + NetMatchSettings.MinMaxPlayers + " and " + NetMatchSettings.MaxMaxPlayers);
                }

                _maxPlayers = value;
            }
        }

        // Bot の Id と重ならない範囲に収めるのは、成績の行や JSON の playerId で Bot と見分けられなくなるため。
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

        public ServerPlayer Join(
            ConnectionId connection,
            string rejoinKey,
            string productUserId = null,
            string partyId = null,
            int? skillRating = null,
            string voiceUserId = null,
            string displayName = null)
        {
            // 受け取った名前は PlayerDisplayName.Sanitize を通してから渡すので、ここで通らない名前は呼ぶ側の誤り
            if (!string.IsNullOrEmpty(displayName) && !PlayerDisplayName.IsValid(displayName, NetMatchSettings.MaxMaxDisplayNameLength))
            {
                throw new ArgumentException("'" + displayName + "' is not a sanitized display name", nameof(displayName));
            }

            if (!string.IsNullOrEmpty(partyId) && !ConnectionExtras.IsValidField(partyId, ConnectionExtras.MaxPartyIdLength))
            {
                throw new ArgumentException(
                    nameof(partyId) + " must be 1-" + ConnectionExtras.MaxPartyIdLength + " characters of [0-9A-Za-z_-]", nameof(partyId));
            }

            if (productUserId != null && !NetLaunchOptions.IsProductUserId(productUserId))
            {
                throw new ArgumentException("'" + productUserId + "' is not an EOS product user id", nameof(productUserId));
            }

            if (voiceUserId != null && !NetLaunchOptions.IsProductUserId(voiceUserId))
            {
                throw new ArgumentException("'" + voiceUserId + "' is not an EOS product user id", nameof(voiceUserId));
            }

            if (!TryGetPlayerId(connection, out var playerId))
            {
                throw new ArgumentException("connection " + connection + " does not fit a player id", nameof(connection));
            }

            if (_byConnection.ContainsKey(connection))
            {
                throw new ArgumentException("connection " + connection + " has already joined", nameof(connection));
            }

            return Add(connection, playerId, rejoinKey, productUserId, partyId, skillRating, voiceUserId, displayName);
        }

        // Bot は接続を持たないので ConnectionId.None で加え、送る側はそれを読み飛ばす。
        public ServerPlayer JoinBot(int playerId)
        {
            if (!BotRoster.IsBotId(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "not a bot player id");
            }

            if (_players.ContainsKey(playerId))
            {
                throw new ArgumentException("player " + playerId + " has already joined", nameof(playerId));
            }

            _botCount++;
            return Add(ConnectionId.None, playerId, null, null, null, null, null, null);
        }

        public bool LeaveBot(int playerId)
        {
            if (!BotRoster.IsBotId(playerId) || !_players.TryGetValue(playerId, out var player))
            {
                return false;
            }

            Remove(player);
            return true;
        }

        private ServerPlayer Add(
            ConnectionId connection,
            int playerId,
            string rejoinKey,
            string productUserId,
            string partyId,
            int? skillRating,
            string voiceUserId,
            string displayName)
        {
            var rejoined = false;
            var fixedTeam = FixedTeam?.Invoke(BotRoster.IsBotId(playerId));
            Team team;
            if (fixedTeam.HasValue)
            {
                // チームは目的が決めるが、入り直しかどうかはログに残すため ChooseTeam にも尋ねる
                if (ChooseTeam != null)
                {
                    ChooseTeam(rejoinKey, out rejoined);
                }

                team = fixedTeam.Value;
            }
            else
            {
                team = ChooseTeam != null
                    ? ChooseTeam(rejoinKey, out rejoined)
                    : TeamAssigner.Choose(CountPlayers(Team.Alpha), CountPlayers(Team.Bravo), AlphaScore, BravoScore);

                // パーティより前のチームを先に見るのは、つなぎ直すたびに試合の途中でチームが変わらないようにするため
                if (!rejoined && !string.IsNullOrEmpty(partyId))
                {
                    team = ChooseWithParty(partyId);
                }
            }

            var player = new ServerPlayer(
                connection, playerId, team, rejoinKey, productUserId, partyId, ++_joinCount, skillRating, voiceUserId, displayName);
            _players.Add(playerId, player);
            if (connection.IsValid)
            {
                _byConnection.Add(connection, player);
            }

            _log.WriteLine(MatchLog.Joined(playerId, team));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.PlayerJoined(_utcNow(), MatchId, playerId, productUserId, team, rejoined));
            }

            PlayerJoined?.Invoke(player);
            TryStart();
            return player;
        }

        private Team ChooseWithParty(string partyId)
        {
            var alphaParty = 0;
            var bravoParty = 0;
            var alphaHumans = 0;
            var bravoHumans = 0;
            foreach (var player in _players.Values)
            {
                if (player.IsBot)
                {
                    continue;
                }

                var alpha = player.Team == Team.Alpha;
                if (alpha)
                {
                    alphaHumans++;
                }
                else
                {
                    bravoHumans++;
                }

                if (string.Equals(player.PartyId, partyId, StringComparison.Ordinal))
                {
                    if (alpha)
                    {
                        alphaParty++;
                    }
                    else
                    {
                        bravoParty++;
                    }
                }
            }

            return TeamAssigner.Choose(
                CountPlayers(Team.Alpha), CountPlayers(Team.Bravo), AlphaScore, BravoScore,
                alphaParty, bravoParty, alphaHumans, bravoHumans, TeamAssigner.TeamCapacity(MaxPlayers));
        }

        public bool Leave(ConnectionId connection)
        {
            if (!_byConnection.TryGetValue(connection, out var player))
            {
                return false;
            }

            Remove(player);
            return true;
        }

        private void Remove(ServerPlayer player)
        {
            if (player.Connection.IsValid)
            {
                _byConnection.Remove(player.Connection);
            }
            else
            {
                _botCount--;
            }

            _players.Remove(player.PlayerId);
            _log.WriteLine(MatchLog.Left(player.PlayerId));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.PlayerLeft(_utcNow(), MatchId, player.PlayerId, player.ProductUserId));
            }

            PlayerLeft?.Invoke(player);
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

        // 試合の JSON のイベントを 1 か所で書き、どれも同じ試合の Id と時計を使うようにする
        public void WriteKill(int killerId, int victimId, bool teamKill, IReadOnlyList<int> assisterIds = null)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.Kill(
                    _utcNow(), MatchId, killerId, ProductUserIdOf(killerId), victimId, ProductUserIdOf(victimId), teamKill, assisterIds));
            }
        }

        public void WritePingPlaced(SharedPing ping, bool refreshed)
        {
            if (_events.Enabled)
            {
                var marker = ping.Marker;
                _events.Write(ServerEventLog.PingPlaced(
                    _utcNow(), MatchId, ping.PingerId, ProductUserIdOf(ping.PingerId), ping.Team, marker.Id, marker.Kind,
                    marker.Kind == PingKind.Enemy ? marker.TargetKey : null, refreshed));
            }
        }

        public void WritePingRefused(int playerId, PingRefusal reason)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.PingRefused(_utcNow(), MatchId, playerId, ProductUserIdOf(playerId), reason));
            }
        }

        public void WriteChat(ChatLine line)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.Chat(_utcNow(), MatchId, line.SenderId, ProductUserIdOf(line.SenderId), line.Channel, line.Text.Length));
            }
        }

        public void WriteChatRefused(int playerId, ChatRefusal reason)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.ChatRefused(_utcNow(), MatchId, playerId, ProductUserIdOf(playerId), reason));
            }
        }

        public void WriteVehicleEntered(int playerId, int vehicle, string vehicleId, int seat, VehicleSeatRole role)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.VehicleEntered(_utcNow(), MatchId, playerId, ProductUserIdOf(playerId), vehicle, vehicleId, seat, role));
            }
        }

        public void WriteVehicleLeft(int playerId, int vehicle, string vehicleId, int seat, VehicleLeaveReason reason)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.VehicleLeft(_utcNow(), MatchId, playerId, ProductUserIdOf(playerId), vehicle, vehicleId, seat, reason));
            }
        }

        public void WriteVehicleExploded(int vehicle, string vehicleId, int? attackerId, int riders, int damaged, int killed)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.VehicleExploded(
                    _utcNow(), MatchId, vehicle, vehicleId, attackerId, attackerId.HasValue ? ProductUserIdOf(attackerId.Value) : null, riders,
                    damaged, killed));
            }
        }

        private string ProductUserIdOf(int playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player.ProductUserId : null;
        }

        // 終わった回の Tick の残りを待ちに数えないのは、Core の TeamDeathmatchSession と同じ間で次の試合を始めるため。
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
            // 終わった試合の Id で記録できるよう、Id を作り直す前に替える
            ChangeMapForNextMatch();
            ChangeRulesForNextMatch();
            BalanceTeamsForNextMatch();
            State = GameModeState.Waiting;
            _restartRemaining = 0f;
            AlphaScore = 0;
            BravoScore = 0;
            ForgetBarredForNextMatch();
            MatchId = ServerEventLog.NewMatchId();
            _log.WriteLine(MatchLog.Restart());
            Restarted?.Invoke();
            TryStart();
        }

        // 中身を ServerMatch.Maps.cs・ServerMatch.Rules.cs・ServerMatch.Balance.cs に分けて書けるよう partial method にする (書かれていなければ何もしない)
        partial void ChangeMapForNextMatch();

        partial void ChangeRulesForNextMatch();

        partial void BalanceTeamsForNextMatch();

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
