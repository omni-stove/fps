using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Execution のとき、Elimination と同じくラウンドに区切り、ラウンドの勝ち負けは EliminationObjective に任せる。
    // オフラインの ExecutionSession と同じく、倒れた人はダウンし、ダウン中の敵はとどめの技 (DownedFinisher) でしか倒せない。
    // とどめは ServerMelee がテイクダウンと同じ ContextMeleeSelector・ContextMeleeRun で決め、倒した知らせ (PlayerKilled) で倒れた人に数える。
    // ダウン中の人は倒れた人に数えないので、全員がとどめを刺されるか出血で倒れた側がラウンドを落とす
    internal sealed class ExecutionRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly EliminationObjective _rounds;
        private readonly ContextMeleeMove _finisher;
        private bool _meleeOn = true;
        private bool _started;
        private bool _hasFinishedRound;
        private Side? _lastRoundWinner;
        private RoundEndReason _lastRoundEnd;

        public ExecutionRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Spec = settings.Execution;
            _finisher = ContextMeleeMove.Execute(Spec);
            try
            {
                _rounds = new EliminationObjective(
                    settings.RoundWinsToWin, settings.RoundPreparationSeconds, settings.RoundTimeLimitSeconds, CountStanding);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: " + e.Message, e);
            }
        }

        public MatchObjective Objective => MatchObjective.Execution;

        public ExecutionSpec Spec { get; }

        public EliminationObjective Rounds => _rounds;

        public bool PlaysInRounds => true;

        // 試合の点数はラウンドの勝ち数にする
        public bool KeepsMatchScore => true;

        // 合間は撃ち合わせない。撃ち始めの秒を全員でそろえるため
        public bool HoldsFire => _rounds.IsPreparing;

        public float? RemainingSeconds => _rounds.RemainingSeconds;

        // 近接が Off のルールでは技を出せず、ダウン中の敵を倒す手が出血だけになるので、とどめを技に限らない
        public ContextMeleeMove DownedFinisher => _meleeOn ? _finisher : null;

        // オフラインの ExecutionRunner と同じく、ルールのダウンが Off でもダウンを使う。
        // 試合全体を時間とキル数で切らず、1 ラウンドの制限時間で区切るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(settings);
            var sessionRules = rules;
            if (rules.Actions.Downed != Downed.Revive)
            {
                sessionRules = rules.WithActions(rules.Actions.WithDowned(Downed.Revive));
                _host.Log.WriteLine(ExecutionRulesLog.DownedForced(rules.Actions.Downed));
            }

            _meleeOn = rules.Actions.Melee == Melee.On;
            if (!_meleeOn)
            {
                _host.Log.WriteLine(ExecutionRulesLog.MeleeOff(rules.Actions.Melee));
            }

            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, sessionRules, respawnsByRound: true);
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
            _rounds.PreparationStarted += HandleRoundPreparing;
            _rounds.RoundStarted += HandleRoundLive;
            _rounds.RoundEnded += HandleRoundEnded;
            var rounds = _rounds.Rounds;
            _host.Log.WriteLine(ExecutionRulesLog.Started(rounds.WinsToWin, rounds.PreparationSeconds, _rounds.Mode.TimeLimitSeconds, Spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _rounds.PreparationStarted -= HandleRoundPreparing;
            _rounds.RoundStarted -= HandleRoundLive;
            _rounds.RoundEnded -= HandleRoundEnded;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            return _rounds.Tick(matchPlaying, deltaSeconds);
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                _rounds.SetPlayer(playerId, session.GetTeam(playerId));
            }
        }

        public void PlayerLeft(int playerId)
        {
            _rounds.RemovePlayer(playerId);
        }

        // ダウンは Session が倒れたと知らせないので、ここに来るのはとどめ・出血・ダウンを飛ばした倒れ方 (全滅・自滅) だけ
        public void PlayerKilled(int killerId, int victimId)
        {
            _rounds.ReportFall(killerId, victimId);
        }

        public void MatchRestarted()
        {
            _rounds.Reset();
            _hasFinishedRound = false;
            _lastRoundWinner = null;
            _lastRoundEnd = default;
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(ExecutionRulesLog.Restart());
        }

        public void MatchEnded()
        {
            _host.Log.WriteLine(ExecutionRulesLog.State(_rounds, CountStanding, CountDowned));
        }

        // 試合が終わった後もラウンドが Live のまま残りうるので、試合の終わりを Ended として送る
        public RoundPhase? CurrentRoundPhase()
        {
            var session = _host.Session;
            return session != null && session.State == GameModeState.Ended ? RoundPhase.Ended : _rounds.Phase;
        }

        public Team? DecideWinner(Team? killWinner)
        {
            return _rounds.DecideWinner();
        }

        // ラウンドの勝ち数を AlphaPoints・BravoPoints に載せ (Elimination と同じ)、ラウンドの番号と前のラウンドの勝者を載せる
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            var rounds = _rounds.Rounds;
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _rounds.GetRoundWins(Team.Alpha);
            snapshot.BravoPoints = _rounds.GetRoundWins(Team.Bravo);
            snapshot.RoundNumber = rounds.RoundNumber;
            snapshot.RoundsToWin = rounds.WinsToWin;
            snapshot.HasLastRound = _hasFinishedRound;
            snapshot.LastRoundEnd = _hasFinishedRound ? _lastRoundEnd : default;
            snapshot.HasLastRoundWinner = _hasFinishedRound && _lastRoundWinner.HasValue;
            snapshot.LastRoundWinner = snapshot.HasLastRoundWinner ? _lastRoundWinner.Value.ToTeam() : Team.Alpha;
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

        // RoundEndReason は Search and Destroy の攻守で名付けてあり、Execution に攻守は無いので、Alpha を攻め側に見立てて全滅した側を表す。
        // 値を足さないのは、足すと Search and Destroy の HUD がその文言を持たなければならなくなるため
        internal static RoundEndReason EndReason(Side? winner, bool timeUp)
        {
            if (timeUp)
            {
                return RoundEndReason.TimeUp;
            }

            if (!winner.HasValue)
            {
                return RoundEndReason.BothEliminated;
            }

            return winner.Value == Side.Alpha ? RoundEndReason.DefendersEliminated : RoundEndReason.AttackersEliminated;
        }

        // ダウン中の人を数えないのは、時間切れの時点で撃ち合えない人だから (ExecutionSession の時間切れと同じ)
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

        private int CountDowned(Team team)
        {
            var session = _host.Session;
            if (session == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (session.GetTeam(playerId) == team && session.IsDowned(playerId))
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
            var side = winner.HasValue ? Side.FromTeam(winner.Value) : (Side?)null;
            _hasFinishedRound = true;
            _lastRoundWinner = side;
            _lastRoundEnd = EndReason(side, !(_rounds.Mode.RemainingSeconds > 0f));
            var alpha = _rounds.GetRoundWins(Team.Alpha);
            var bravo = _rounds.GetRoundWins(Team.Bravo);
            _host.Log.WriteLine(MatchLog.RoundEnded(round, winner, alpha, bravo));
            _host.SetMatchScores(alpha, bravo);
        }
    }
}
