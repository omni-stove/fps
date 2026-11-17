using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class SkillSpec
    {
        public const int DefaultInitialRating = 1000;

        public const float DefaultKFactor = 32f;

        public const int DefaultMinRating = 0;

        public const int DefaultMaxRating = 3000;

        public static SkillSpec Default { get; } = new SkillSpec();

        public SkillSpec(
            int initialRating = DefaultInitialRating,
            float kFactor = DefaultKFactor,
            int minRating = DefaultMinRating,
            int maxRating = DefaultMaxRating)
        {
            KFactor = SpecGuard.PositiveFinite(kFactor, nameof(kFactor));
            MinRating = SpecGuard.NonNegative(minRating, nameof(minRating));
            if (maxRating <= minRating)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRating), maxRating, $"maxRating must be more than minRating ({minRating.ToString(CultureInfo.InvariantCulture)})");
            }

            MaxRating = maxRating;
            if (initialRating < minRating || initialRating > maxRating)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialRating),
                    initialRating,
                    $"initialRating must be from minRating ({minRating.ToString(CultureInfo.InvariantCulture)}) "
                    + $"to maxRating ({maxRating.ToString(CultureInfo.InvariantCulture)})");
            }

            InitialRating = initialRating;
        }

        public int InitialRating { get; }

        public float KFactor { get; }

        public int MinRating { get; }

        public int MaxRating { get; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "initial={0} k={1} min={2} max={3}", InitialRating, KFactor, MinRating, MaxRating);
        }
    }
}
