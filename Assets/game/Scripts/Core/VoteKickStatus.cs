using System;
using System.Globalization;

namespace Shooter.Core
{
    // 対象と言い出した人を名前でなく参加者の Id で持つのは、名前に引くのを UI (IParticipants) に任せるため。
    public readonly struct VoteKickStatus : IEquatable<VoteKickStatus>
    {
        public VoteKickStatus(int targetId, int callerId, int yesVotes, int requiredVotes, float remainingSeconds)
        {
            if (targetId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), targetId, "targetId must not be negative");
            }

            if (callerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(callerId), callerId, "callerId must not be negative");
            }

            if (yesVotes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(yesVotes), yesVotes, "yesVotes must not be negative");
            }

            if (requiredVotes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredVotes), requiredVotes, "requiredVotes must be at least 1");
            }

            if (!(remainingSeconds >= 0f) || float.IsInfinity(remainingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds), remainingSeconds, "remainingSeconds must be finite and not negative");
            }

            TargetId = targetId;
            CallerId = callerId;
            YesVotes = yesVotes;
            RequiredVotes = requiredVotes;
            RemainingSeconds = remainingSeconds;
        }

        public int TargetId { get; }

        public int CallerId { get; }

        public int YesVotes { get; }

        public int RequiredVotes { get; }

        public float RemainingSeconds { get; }

        // default は RequiredVotes が 0 でコンストラクタを通らないので、要る数が 1 以上のときだけ足りたとみなす。
        public bool HasEnoughVotes => RequiredVotes > 0 && YesVotes >= RequiredVotes;

        public bool Equals(VoteKickStatus other)
        {
            return TargetId == other.TargetId
                && CallerId == other.CallerId
                && YesVotes == other.YesVotes
                && RequiredVotes == other.RequiredVotes
                && RemainingSeconds.Equals(other.RemainingSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is VoteKickStatus other && Equals(other);
        }

        public static bool operator ==(VoteKickStatus left, VoteKickStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoteKickStatus left, VoteKickStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TargetId, CallerId, YesVotes, RequiredVotes, RemainingSeconds);
        }

        public override string ToString()
        {
            return "target=" + TargetId.ToString(CultureInfo.InvariantCulture)
                + " caller=" + CallerId.ToString(CultureInfo.InvariantCulture)
                + " yes=" + YesVotes.ToString(CultureInfo.InvariantCulture) + "/" + RequiredVotes.ToString(CultureInfo.InvariantCulture)
                + " left=" + RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
