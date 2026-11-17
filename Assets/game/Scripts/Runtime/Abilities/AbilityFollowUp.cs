using System;

namespace Fps.Runtime
{
    /// <summary>
    /// 能力の枠で今もう一度押すと何ができるか (<see cref="IAbilityFollowUp.TryGetFollowUp"/>)。何をどう言うかは UI が決める。
    /// </summary>
    public readonly struct AbilityFollowUp : IEquatable<AbilityFollowUp>
    {
        /// <exception cref="ArgumentOutOfRangeException">count が負。</exception>
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

        /// <summary>効果を受ける物の種類の Id (起爆なら置いた物の種類)。</summary>
        public string TargetId { get; }

        /// <summary>効果を受ける物の名前を引く表のキー。空なら名前は <see cref="TargetId"/>。</summary>
        public string TargetNameKey { get; }

        /// <summary>効果を受ける物の数 (起爆なら置いている数)。</summary>
        public int Count { get; }

        /// <summary>targetId の置いた物 count 個を起爆できる。</summary>
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
