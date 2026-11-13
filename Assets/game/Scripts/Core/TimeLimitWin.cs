using System;

namespace Fps.Core
{
    /// <summary>時間切れで、得点がいちばん多い陣営の勝ち。いちばん多い陣営が複数あれば引き分け (<see cref="ModeSession.LeadingSide"/>)。</summary>
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
