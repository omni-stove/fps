using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 受けた VoteKickStateMessage の写し。メッセージの表は読むたびに同じ物を上書きするので、出来事には値で渡す
    public readonly struct ClientVoteKickState
    {
        public ClientVoteKickState(int targetPlayerId, int callerPlayerId, int yesVotes, int requiredVotes, float remainingSeconds, VoteKickOutcome outcome)
        {
            TargetPlayerId = targetPlayerId;
            CallerPlayerId = callerPlayerId;
            YesVotes = yesVotes;
            RequiredVotes = requiredVotes;
            RemainingSeconds = remainingSeconds;
            Outcome = outcome;
        }

        public int TargetPlayerId { get; }

        public int CallerPlayerId { get; }

        public int YesVotes { get; }

        public int RequiredVotes { get; }

        public float RemainingSeconds { get; }

        public VoteKickOutcome Outcome { get; }

        public static ClientVoteKickState From(VoteKickStateMessage message)
        {
            return new ClientVoteKickState(
                message.TargetPlayerId, message.CallerPlayerId, message.YesVotes, message.RequiredVotes, message.RemainingSeconds, message.Outcome);
        }
    }
}
