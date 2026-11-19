using System;
using System.Numerics;

namespace Shooter.Core
{
    // 高さを乗り物の原点ではなく当たり判定の底から測るのは、屋上の固定銃座など何かに載っている乗り物を脱出にしないため。
    public sealed class BailOutSpec
    {
        // ParachuteMotion.DefaultMinOpenHeight より高くし、脱出したらパラシュートが開ける高さにいるようにしてある。
        public const float DefaultMinHeight = 10f;

        public const float DefaultInheritedSpeedFraction = 0.3f;

        public const float DefaultExitHeight = 1f;

        public const float DefaultExitSideDistance = 1f;

        public BailOutSpec(
            float minHeight = DefaultMinHeight,
            float inheritedSpeedFraction = DefaultInheritedSpeedFraction,
            float exitHeight = DefaultExitHeight,
            float exitSideDistance = DefaultExitSideDistance,
            bool enabled = true)
        {
            if (!(minHeight > 0f) || float.IsInfinity(minHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(minHeight), minHeight, "minHeight must be positive and finite");
            }

            if (!(inheritedSpeedFraction >= 0f) || inheritedSpeedFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(inheritedSpeedFraction), inheritedSpeedFraction, "inheritedSpeedFraction must be between 0 and 1");
            }

            if (!(exitHeight >= 0f) || float.IsInfinity(exitHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(exitHeight), exitHeight, "exitHeight must be finite and not negative");
            }

            if (!(exitSideDistance >= 0f) || float.IsInfinity(exitSideDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitSideDistance), exitSideDistance, "exitSideDistance must be finite and not negative");
            }

            MinHeight = minHeight;
            InheritedSpeedFraction = inheritedSpeedFraction;
            ExitHeight = exitHeight;
            ExitSideDistance = exitSideDistance;
            Enabled = enabled;
        }

        public static BailOutSpec Default { get; } = new BailOutSpec();

        public float MinHeight { get; }

        public float InheritedSpeedFraction { get; }

        public float ExitHeight { get; }

        public float ExitSideDistance { get; }

        public bool Enabled { get; }

        public BailOutVerdict Evaluate(float? heightAboveGround)
        {
            if (heightAboveGround.HasValue && !(heightAboveGround.Value >= 0f && !float.IsPositiveInfinity(heightAboveGround.Value)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(heightAboveGround), heightAboveGround, "heightAboveGround must be finite and not negative; pass null when no ground was found");
            }

            if (!Enabled)
            {
                return BailOutVerdict.Off;
            }

            if (!heightAboveGround.HasValue)
            {
                return BailOutVerdict.NoGround;
            }

            return heightAboveGround.Value < MinHeight ? BailOutVerdict.TooLow : BailOutVerdict.BailOut;
        }

        public Vector3 InheritedVelocity(Vector3 vehicleVelocity)
        {
            if (!IsFinite(vehicleVelocity.X) || !IsFinite(vehicleVelocity.Y) || !IsFinite(vehicleVelocity.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(vehicleVelocity), vehicleVelocity, "vehicleVelocity must be finite");
            }

            return vehicleVelocity * InheritedSpeedFraction;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
