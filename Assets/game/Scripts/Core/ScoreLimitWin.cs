using System;

namespace Fps.Core
{
    /// <summary>得点が <see cref="ScoreLimit"/> に届いた陣営の勝ち。</summary>
    public sealed class ScoreLimitWin : IWinCondition
    {
        /// <summary>勝利に必要な得点。0 より大きい有限値。</summary>
        public double? ScoreLimit { get; }

        /// <param name="scoreLimit">勝利に必要な得点。0 より大きい有限値。</param>
        public ScoreLimitWin(double scoreLimit)
        {
            if (!(scoreLimit > 0d) || double.IsInfinity(scoreLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be positive and finite");
            }

            ScoreLimit = scoreLimit;
        }

        public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

        public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
    }
}
