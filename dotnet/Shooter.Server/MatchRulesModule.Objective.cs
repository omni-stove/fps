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
        private IMatchObjectiveRules _objective;

        internal IMatchObjectiveRules Objective => _objective;

        // 拠点制圧の試合でなければ null
        internal DominationObjective Domination => (_objective as DominationRules)?.Domination;

        // ラウンド制の試合でなければ null
        internal EliminationObjective Elimination => (_objective as EliminationRules)?.Elimination;

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

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
            _objective?.FillControlPoints(points);
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
