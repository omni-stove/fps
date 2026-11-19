using System;

namespace Shooter.Core
{
    public sealed class AircraftWarningSpec
    {
        public const float DefaultStallAngleDegrees = 15f;
        public const float DefaultStallSpeed = 30f;

        public AircraftWarningSpec(float stallAngleDegrees = DefaultStallAngleDegrees, float stallSpeed = DefaultStallSpeed)
        {
            if (!(stallAngleDegrees > 0f && stallAngleDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(stallAngleDegrees), stallAngleDegrees, "stallAngleDegrees must be above 0 and at most 90");
            }

            if (!(stallSpeed >= 0f) || float.IsInfinity(stallSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(stallSpeed), stallSpeed, "stallSpeed must be a non-negative finite number");
            }

            StallAngleDegrees = stallAngleDegrees;
            StallSpeed = stallSpeed;
        }

        public static AircraftWarningSpec Default => new AircraftWarningSpec();

        // 迎え角の向きを問わずこの角度から警告するのは、背面や押し込みで翼が逆から空気を受けても同じように失速するため。
        public float StallAngleDegrees { get; }

        public float StallSpeed { get; }
    }
}
