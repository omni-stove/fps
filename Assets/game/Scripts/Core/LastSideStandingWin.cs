using System;

namespace Fps.Core
{
    /// <summary>
    /// 脱落した陣営 (<see cref="ModeSession.IsSideEliminated"/>: 1 人以上いて全員が戻らない形で倒れている) があり、
    /// 脱落していない人のいる陣営が 1 つ以下になったら終える。残った 1 陣営の勝ちで、得点は見ない。
    /// 人のいる陣営が 1 つも残らなかったときは、チーム分けの <see cref="ITeamLayout.FixedSides"/> のうち脱落していない (誰もいない) 陣営が
    /// ちょうど 1 つならその陣営の勝ち、そうでなければ引き分け。
    /// </summary>
    /// <remarks>
    /// 誰もいない始めからの陣営を最後にだけ数えるのは、2 チームで片方が全員抜けた後にもう片方が全滅したら、
    /// 抜けた側の勝ちにする扱いを保つため。FFA で抜けた人の陣営は数えない。
    /// </remarks>
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
