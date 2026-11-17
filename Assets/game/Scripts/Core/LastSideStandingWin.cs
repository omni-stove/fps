using System;

namespace Shooter.Core
{
    public sealed class LastSideStandingWin : IWinCondition
    {
        public double? ScoreLimit => null;

        public ModeOutcome CheckStanding(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var anyEliminated = false;
            var standingCount = 0;
            Side standing = default;
            foreach (var side in session.Sides)
            {
                if (session.CountMembers(side) == 0)
                {
                    continue;
                }

                if (session.IsSideEliminated(side))
                {
                    anyEliminated = true;
                }
                else
                {
                    standingCount++;
                    standing = side;
                }
            }

            if (!anyEliminated || standingCount > 1)
            {
                return ModeOutcome.Undecided;
            }

            if (standingCount == 1)
            {
                return ModeOutcome.Win(standing);
            }

            // 誰もいない始めからの陣営を最後にだけ数えるのは、2 チームで片方が全員抜けた後にもう片方が全滅したら、
            // 抜けた側の勝ちにする扱いを保つため。
            var emptyCount = 0;
            Side empty = default;
            foreach (var side in session.Layout.FixedSides)
            {
                if (!session.IsSideEliminated(side))
                {
                    emptyCount++;
                    empty = side;
                }
            }

            return emptyCount == 1 ? ModeOutcome.Win(empty) : ModeOutcome.Draw;
        }

        public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
    }
}
