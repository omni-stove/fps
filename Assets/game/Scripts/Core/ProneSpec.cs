using System;

namespace Shooter.Core
{
    public sealed class ProneSpec
    {
        // 半径 0.5m のプレイヤーが CharacterController で取れる最も低いカプセル (直径と同じ高さ)。
        public const float DefaultHeight = 1f;

        public const float DefaultSpeed = 1.2f;

        public const float DefaultMaxPitchDegrees = 30f;

        public const float DefaultSpreadScale = 0.5f;

        public const float DefaultDiveDistance = 2.5f;

        public const float DefaultDiveSeconds = 0.5f;

        public static readonly ProneSpec Default = new ProneSpec();

        public float Height { get; }

        public float Speed { get; }

        public float MaxPitchDegrees { get; }

        public float SpreadScale { get; }

        public float DiveDistance { get; }

        public float DiveSeconds { get; }

        // しゃがみより低いことと半径の 2 倍以上であることは、体の寸法を知る StanceGeometry が確かめる。
        public ProneSpec(
            float height = DefaultHeight,
            float speed = DefaultSpeed,
            float maxPitchDegrees = DefaultMaxPitchDegrees,
            float spreadScale = DefaultSpreadScale,
            float diveDistance = DefaultDiveDistance,
            float diveSeconds = DefaultDiveSeconds)
        {
            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be finite and positive");
            }

            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and not negative");
            }

            if (!(maxPitchDegrees > 0f && maxPitchDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPitchDegrees), maxPitchDegrees, "maxPitchDegrees must be in (0, 90]");
            }

            if (!(spreadScale >= 0f && spreadScale <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(spreadScale), spreadScale, "spreadScale must be between 0 and 1");
            }

            if (!(diveDistance >= 0f) || float.IsInfinity(diveDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(diveDistance), diveDistance, "diveDistance must be finite and not negative");
            }

            if (!(diveSeconds > 0f) || float.IsInfinity(diveSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(diveSeconds), diveSeconds, "diveSeconds must be finite and positive");
            }

            Height = height;
            Speed = speed;
            MaxPitchDegrees = maxPitchDegrees;
            SpreadScale = spreadScale;
            DiveDistance = diveDistance;
            DiveSeconds = diveSeconds;
        }
    }
}
