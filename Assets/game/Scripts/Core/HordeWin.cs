using System;

namespace Shooter.Core
{
    public sealed class HordeWin : IWinCondition
    {
        private readonly WaveSchedule _schedule;

        public HordeWin(WaveSchedule schedule, Side defenders, Side attackers)
        {
            if (defenders == attackers)
            {
                throw new ArgumentException($"defenders and attackers must differ, but both are {defenders}", nameof(attackers));
            }

            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            Defenders = defenders;
            Attackers = attackers;
        }

        public Side Defenders { get; }

        public Side Attackers { get; }

        public double? ScoreLimit => null;

        public ModeOutcome CheckStanding(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            // 脱落より先に見るのは、最後の敵を倒したのと同じ間に最後の 1 人が倒れたときを守る側の勝ちにするため。
            if (_schedule.Phase == WavePhase.Completed)
            {
                return ModeOutcome.Win(Defenders);
            }

            return session.IsSideEliminated(Defenders) ? ModeOutcome.Win(Attackers) : ModeOutcome.Undecided;
        }

        public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
    }
}
