using System;

namespace Shooter.Runtime
{
    public readonly struct AbilityFollowUp : IEquatable<AbilityFollowUp>
    {
        public AbilityFollowUp(AbilityFollowUpKind kind, string targetId, string targetNameKey, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            Kind = kind;
            TargetId = targetId ?? string.Empty;
            TargetNameKey = targetNameKey != null ? targetNameKey.Trim() : string.Empty;
            Count = count;
        }

        public AbilityFollowUpKind Kind { get; }

        public string TargetId { get; }

        public string TargetNameKey { get; }

        public int Count { get; }

        public static AbilityFollowUp Detonate(string targetId, string targetNameKey, int count)
        {
            return new AbilityFollowUp(AbilityFollowUpKind.Detonate, targetId, targetNameKey, count);
        }

        public bool Equals(AbilityFollowUp other)
        {
            return Kind == other.Kind
                && string.Equals(TargetId ?? string.Empty, other.TargetId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(TargetNameKey ?? string.Empty, other.TargetNameKey ?? string.Empty, StringComparison.Ordinal)
                && Count == other.Count;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityFollowUp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TargetNameKey ?? string.Empty);
                return (hash * 397) ^ Count;
            }
        }

        public static bool operator ==(AbilityFollowUp left, AbilityFollowUp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbilityFollowUp left, AbilityFollowUp right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Kind} {TargetId} x{Count}";
        }
    }
}
