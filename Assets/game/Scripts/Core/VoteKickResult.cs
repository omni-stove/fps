namespace Shooter.Core
{
    public readonly struct VoteKickResult
    {
        public VoteKickResult(int targetId, int callerId, int yesVotes, int noVotes, int requiredVotes, VoteKickEnd end)
        {
            TargetId = targetId;
            CallerId = callerId;
            YesVotes = yesVotes;
            NoVotes = noVotes;
            RequiredVotes = requiredVotes;
            End = end;
        }

        public int TargetId { get; }

        public int CallerId { get; }

        public int YesVotes { get; }

        public int NoVotes { get; }

        public int RequiredVotes { get; }

        public VoteKickEnd End { get; }

        public bool Passed => End == VoteKickEnd.Passed;
    }
}
