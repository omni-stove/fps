using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct StreakTargetingStatus : IEquatable<StreakTargetingStatus>
    {
        public StreakTargetingStatus(
            string rewardId,
            string displayNameKey,
            Vector3 origin,
            Vector3 target,
            float headingDegrees,
            float range,
            float lineLength)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("rewardId must not be empty", nameof(rewardId));
            }

            RewardId = rewardId;
            DisplayNameKey = displayNameKey ?? string.Empty;
            Origin = MinimapContact.RequireFinite(origin, nameof(origin));
            Target = MinimapContact.RequireFinite(target, nameof(target));
            HeadingDegrees = MinimapContact.NormalizeHeading(headingDegrees, nameof(headingDegrees));
            Range = SpecGuard.PositiveFinite(range, nameof(range));
            LineLength = SpecGuard.NonNegativeFinite(lineLength, nameof(lineLength));
        }

        public string RewardId { get; }

        public string DisplayNameKey { get; }

        public Vector3 Origin { get; }

        public Vector3 Target { get; }

        public float HeadingDegrees { get; }

        public float Range { get; }

        public float LineLength { get; }

        public bool Equals(StreakTargetingStatus other)
        {
            return string.Equals(RewardId, other.RewardId, StringComparison.Ordinal)
                && string.Equals(DisplayNameKey, other.DisplayNameKey, StringComparison.Ordinal)
                && Origin.Equals(other.Origin)
                && Target.Equals(other.Target)
                && HeadingDegrees.Equals(other.HeadingDegrees)
                && Range.Equals(other.Range)
                && LineLength.Equals(other.LineLength);
        }

        public override bool Equals(object obj)
        {
            return obj is StreakTargetingStatus other && Equals(other);
        }

        public static bool operator ==(StreakTargetingStatus left, StreakTargetingStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StreakTargetingStatus left, StreakTargetingStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RewardId, DisplayNameKey, Origin, Target, HeadingDegrees, Range, LineLength);
        }

        public override string ToString()
        {
            return RewardId + " target " + MinimapContact.DescribePose(Target, HeadingDegrees)
                + " range " + Range.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
