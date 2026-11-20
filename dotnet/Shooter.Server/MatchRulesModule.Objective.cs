using System;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 目的 (GameRules.Objective) ごとの点数と終わり方は ServerObjectiveFactory が選ぶ IMatchObjectiveRules に任せ、
    // ここでは試合の出来事をそれへ渡す
    internal sealed partial class MatchRulesModule : IMatchObjectiveHost
    {
        private readonly Func<MatchObjective, NetMatchSettings, IMatchObjectiveHost, IMatchObjectiveRules> _createObjective;
        private IMatchObjectiveRules _objective;

        public MatchRulesModule()
            : this(ServerObjectiveFactory.Create)
        {
        }

        // ServerObjectiveFactory に登録する前の目的の実装でも、試合を通して試せるよう選び方を差し替えられる口
        internal MatchRulesModule(Func<MatchObjective, NetMatchSettings, IMatchObjectiveHost, IMatchObjectiveRules> createObjective)
        {
            _createObjective = createObjective ?? throw new ArgumentNullException(nameof(createObjective));
        }

        internal IMatchObjectiveRules Objective => _objective;

        // 拠点制圧の試合でなければ null
        internal DominationObjective Domination => (_objective as DominationRules)?.Domination;

        // ラウンド制の試合でなければ null
        internal EliminationObjective Elimination => (_objective as EliminationRules)?.Elimination;

        // 爆弾設置の試合でなければ null
        internal SearchAndDestroy SearchAndDestroy => (_objective as SearchAndDestroyRules)?.Game;

        private bool IsBetweenRounds => _objective != null && _objective.HoldsFire;

        ServerBodies IMatchObjectiveHost.Bodies => _bodies;

        TextWriter IMatchObjectiveHost.Log => _log;

        void IMatchObjectiveHost.SetMatchScores(int alpha, int bravo)
        {
            _match.SetScores(alpha, bravo);
        }

        void IMatchObjectiveHost.ClearDowned()
        {
            ClearDowned();
        }

        // 試合に居ない人は陣営が無いので、敵味方を決められない。呼ぶ側が先に Session.HasPlayer で確かめる
        internal bool AreHostile(int a, int b)
        {
            if (_session == null || !_session.HasPlayer(a) || !_session.HasPlayer(b))
            {
                throw new InvalidOperationException("both players must be in the match to tell whether they are hostile");
            }

            var aTeam = _session.GetTeam(a);
            var bTeam = _session.GetTeam(b);
            return _objective != null ? _objective.AreHostile(a, aTeam, b, bTeam) : aTeam != bTeam;
        }

        // 試合に居ない人はどちら側でも味方に数えない。見えるかを決めるのに、陣営の分からない人の目を使ったり、その人から隠さずにおいたりしないため
        internal bool AreAllies(int a, int b)
        {
            return _session != null && _session.HasPlayer(a) && _session.HasPlayer(b) && _session.AreAllies(a, b);
        }

        internal bool IsAlive(int playerId)
        {
            return _session != null && _session.HasPlayer(playerId) && _session.IsAlive(playerId);
        }

        // 敵でも味方でもない 2 人 (BR の分隊に入れなかった人と誰か)。Session は味方でない人を敵として撃たせるので、ここで止める
        private bool AreNeutral(int a, int b)
        {
            if (a == b || _objective == null)
            {
                return false;
            }

            var aTeam = _session.GetTeam(a);
            var bTeam = _session.GetTeam(b);
            return !_objective.AreHostile(a, aTeam, b, bTeam) && !_objective.AreAllies(a, aTeam, b, bTeam);
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
            _objective?.FillControlPoints(points);
        }

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            _objective?.FillObjectiveItems(items);
        }

        private void StartObjective()
        {
            _objective.Start();
            if (_objective.PlaysInRounds)
            {
                RoundPhaseSource = _objective.CurrentRoundPhase;
            }
        }

        private void StopObjective()
        {
            if (_objective == null)
            {
                return;
            }

            _objective.Stop();
            var source = RoundPhaseSource;
            if (source != null && ReferenceEquals(source.Target, _objective))
            {
                RoundPhaseSource = null;
            }

            _objective = null;
        }

        private void TickObjective(float deltaSeconds)
        {
            if (_objective.Tick(_session.State == GameModeState.Playing, deltaSeconds))
            {
                // 続く HandleEnded の DecideWinner が同じ勝者を選ぶ
                _session.Finish(_objective.DecideWinner(_session.Winner));
            }
        }

        private void ObjectivePlayerJoined(int playerId)
        {
            _objective.PlayerJoined(playerId);
        }

        // 目的の側で勝者を決めない試合なら、Session がキル数で決めた値のまま
        private Team? DecideWinner(Team? winner)
        {
            return _objective != null ? _objective.DecideWinner(winner) : winner;
        }
    }
}
