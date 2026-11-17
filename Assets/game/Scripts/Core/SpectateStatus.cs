using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>ネット対戦で味方を見ている間の状態。見ている人は参加者の Id で持ち、名前に引くのは UI (<see cref="IParticipants"/>)。</summary>
    public readonly struct SpectateStatus : IEquatable<SpectateStatus>
    {
        /// <param name="targetId">見ている参加者の Id。見られる味方がいなければ null。</param>
        /// <param name="candidateCount">切り替えて見られる人の数。</param>
        /// <exception cref="ArgumentOutOfRangeException">candidateCount が負か、targetId があるのに 0、reason が定義に無い値。</exception>
        public SpectateStatus(int? targetId, int candidateCount, SpectateReason reason)
        {
            if (candidateCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "candidateCount must not be negative");
            }

            if (targetId.HasValue && candidateCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "candidateCount must count the watched target");
            }

            if (!Enum.IsDefined(typeof(SpectateReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined SpectateReason");
            }

            TargetId = targetId;
            CandidateCount = candidateCount;
            Reason = reason;
        }

        public int? TargetId { get; }

        public int CandidateCount { get; }

        public SpectateReason Reason { get; }

        public bool Equals(SpectateStatus other)
        {
            return TargetId == other.TargetId && CandidateCount == other.CandidateCount && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            return obj is SpectateStatus other && Equals(other);
        }

        public static bool operator ==(SpectateStatus left, SpectateStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SpectateStatus left, SpectateStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TargetId, CandidateCount, (int)Reason);
        }

        public override string ToString()
        {
            return "target=" + (TargetId.HasValue ? TargetId.Value.ToString(CultureInfo.InvariantCulture) : "none")
                + " candidates=" + CandidateCount.ToString(CultureInfo.InvariantCulture)
                + " reason=" + Reason;
        }
    }
}
