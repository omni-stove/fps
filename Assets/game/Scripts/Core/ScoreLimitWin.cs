using System;

namespace Shooter.Core
{
    public sealed class ScoreLimitWin : IWinCondition
    {
        public double? ScoreLimit { get; }

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
