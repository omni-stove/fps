using System;

namespace Shooter.Core
{
    public sealed class TimeLimitWin : IWinCondition
    {
        public double? ScoreLimit => null;

        public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

        public ModeOutcome CheckTimeUp(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return ModeOutcome.Decided(session.LeadingSide);
        }
    }
}
