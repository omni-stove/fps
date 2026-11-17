using System;

namespace Shooter.Core
{
    public sealed class DamageFalloff
    {
        public static DamageFalloff None { get; } = new DamageFalloff(float.MaxValue, float.MaxValue, 1f);

        public float StartDistance { get; }

        public float EndDistance { get; }

        public float MinMultiplier { get; }

        public DamageFalloff(float startDistance, float endDistance, float minMultiplier)
        {
            if (!IsFinite(startDistance) || startDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(startDistance), startDistance, "startDistance must be finite and not negative");
            }

            if (!IsFinite(endDistance) || endDistance < startDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(endDistance), endDistance, "endDistance must be finite and not less than startDistance");
            }

            if (float.IsNaN(minMultiplier) || minMultiplier < 0f || minMultiplier > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(minMultiplier), minMultiplier, "minMultiplier must be between 0 and 1");
            }

            StartDistance = startDistance;
            EndDistance = endDistance;
            MinMultiplier = minMultiplier;
        }

        public double MultiplierAt(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be negative");
            }

            if (distance <= StartDistance)
            {
                return 1.0;
            }

            if (distance >= EndDistance)
            {
                return MinMultiplierAsDouble();
            }

            var t = ((double)distance - StartDistance) / ((double)EndDistance - StartDistance);
            return 1.0 + ((MinMultiplierAsDouble() - 1.0) * t);
        }

        // float を 10 進経由で広げ、0.7f が 0.699999988... にならないようにする。
        private double MinMultiplierAsDouble()
        {
            return (double)(decimal)MinMultiplier;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
