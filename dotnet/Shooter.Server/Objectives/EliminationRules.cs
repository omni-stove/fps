using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Elimination のとき、試合をラウンドに区切る。Session (TeamDeathmatchSession) は倒れた人を出直させず、
    // 全滅でも試合を終えない形で作り、ラウンドの勝ち負けは EliminationObjective が決める。勝ち数が上限に届いたら試合を終える。
    // 試合全体の制限時間は設けず、1 ラウンドの制限時間で区切る
    internal sealed class EliminationRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly EliminationObjective _elimination;
        private bool _started;

        public EliminationRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            try
            {
                _elimination = new EliminationObjective(
                    settings.RoundWinsToWin, settings.RoundPreparationSeconds, settings.RoundTimeLimitSeconds, CountStanding);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: " + e.Message, e);
            }
        }

        public MatchObjective Objective => MatchObjective.Elimination;

        public EliminationObjective Elimination => _elimination;

        public bool PlaysInRounds => true;

        // 試合の点数はラウンドの勝ち数にする
        public bool KeepsMatchScore => true;

        // 合間は撃ち合わせない。撃ち始めの秒を全員でそろえるため
        public bool HoldsFire => _elimination.IsPreparing;

        public float? RemainingSeconds => _elimination.RemainingSeconds;

        // 試合全体を時間とキル数で切らず、1 ラウンドの制限時間で区切るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, rules, respawnsByRound: true);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _elimination.PreparationStarted += HandleRoundPreparing;
            _elimination.RoundStarted += HandleRoundLive;
            _elimination.RoundEnded += HandleRoundEnded;
            var rounds = _elimination.Rounds;
            _host.Log.WriteLine(MatchLog.Elimination(rounds.WinsToWin, rounds.PreparationSeconds, _elimination.Mode.TimeLimitSeconds));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _elimination.PreparationStarted -= HandleRoundPreparing;
            _elimination.RoundStarted -= HandleRoundLive;
            _elimination.RoundEnded -= HandleRoundEnded;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            return _elimination.Tick(matchPlaying, deltaSeconds);
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                _elimination.SetPlayer(playerId, session.GetTeam(playerId));
            }
        }

        public void PlayerLeft(int playerId)
        {
            _elimination.RemovePlayer(playerId);
        }

        public void PlayerKilled(int killerId, int victimId)
        {
            _elimination.ReportFall(killerId, victimId);
        }

        public void MatchRestarted()
        {
            _elimination.Reset();
            _host.SetMatchScores(0, 0);
        }

        public void MatchEnded()
        {
        }

        // 試合が終わった後もラウンドが Live のまま残りうるので、試合の終わりを Ended として送る
        public RoundPhase? CurrentRoundPhase()
        {
            var session = _host.Session;
            return session != null && session.State == GameModeState.Ended ? RoundPhase.Ended : _elimination.Phase;
        }

        public Team? DecideWinner(Team? killWinner)
        {
            return _elimination.DecideWinner();
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _elimination.GetRoundWins(Team.Alpha);
            snapshot.BravoPoints = _elimination.GetRoundWins(Team.Bravo);
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        // ダウン中の人を数えないのは、時間切れの時点で撃ち合えない人だから
        private int CountStanding(Team team)
        {
            var session = _host.Session;
            if (session == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (session.GetTeam(playerId) == team && session.IsAlive(playerId) && !session.IsDowned(playerId))
                {
                    count++;
                }
            }

            return count;
        }

        // 体力と位置は Session が持つので、ここで全員を出直させる。倒れている人の蘇生の受け付けも捨てる
        private void HandleRoundPreparing(int round)
        {
            _host.Log.WriteLine(MatchLog.RoundPreparing(round));
            _host.ClearDowned();
            _host.Session.RespawnAll();
        }

        private void HandleRoundLive(int round)
        {
            _host.Log.WriteLine(MatchLog.RoundLive(round));
        }

        private void HandleRoundEnded(int round, Team? winner)
        {
            var alpha = _elimination.GetRoundWins(Team.Alpha);
            var bravo = _elimination.GetRoundWins(Team.Bravo);
            _host.Log.WriteLine(MatchLog.RoundEnded(round, winner, alpha, bravo));
            _host.SetMatchScores(alpha, bravo);
        }
    }
}
