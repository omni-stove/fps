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

            var winner = Decide(_schedule.Phase, session.IsSideEliminated(Defenders), Defenders, Attackers);
            return winner.HasValue ? ModeOutcome.Win(winner.Value) : ModeOutcome.Undecided;
        }

        public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;

        // ModeSession を受け取らないのは、専用サーバーの HordeRules (TeamDeathmatchSession) も同じ順で勝ち負けを決めるため。
        // 脱落より先に見るのは、最後の敵を倒したのと同じ間に最後の 1 人が倒れたときを守る側の勝ちにするため。
        public static Side? Decide(WavePhase phase, bool defendersOut, Side defenders, Side attackers)
        {
            if (defenders == attackers)
            {
                throw new ArgumentException($"defenders and attackers must differ, but both are {defenders}", nameof(attackers));
            }

            if (phase == WavePhase.Completed)
            {
                return defenders;
            }

            return defendersOut ? attackers : (Side?)null;
        }
    }
}
