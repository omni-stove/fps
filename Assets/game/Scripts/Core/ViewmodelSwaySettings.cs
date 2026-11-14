using System;

namespace Fps.Core
{
    public sealed class ViewmodelSwaySettings
    {
        public const float DefaultBobHorizontalMeters = 0.012f;
        public const float DefaultBobVerticalMeters = 0.008f;
        public const float DefaultBobCyclesPerMeter = 0.55f;
        public const float DefaultBobReferenceSpeed = 5f;
        public const float DefaultBobSharpness = 8f;
        public const float DefaultLookLagSeconds = 0.02f;
        public const float DefaultMaxLookLagDegrees = 5f;
        public const float DefaultLookLagSharpness = 12f;
        public const float DefaultKickBackMeters = 0.03f;
        public const float DefaultKickPitchDegrees = 2f;
        public const float DefaultKickReturnPerSecond = 12f;
        public const float DefaultMaxKicks = 3f;
        public const float DefaultAdsSwayScale = 0.15f;
        public const float DefaultAdsKickScale = 0.4f;

        public static readonly ViewmodelSwaySettings Default = new ViewmodelSwaySettings();

        public ViewmodelSwaySettings(
            float bobHorizontalMeters = DefaultBobHorizontalMeters,
            float bobVerticalMeters = DefaultBobVerticalMeters,
            float bobCyclesPerMeter = DefaultBobCyclesPerMeter,
            float bobReferenceSpeed = DefaultBobReferenceSpeed,
            float bobSharpness = DefaultBobSharpness,
            float lookLagSeconds = DefaultLookLagSeconds,
            float maxLookLagDegrees = DefaultMaxLookLagDegrees,
            float lookLagSharpness = DefaultLookLagSharpness,
            float kickBackMeters = DefaultKickBackMeters,
            float kickPitchDegrees = DefaultKickPitchDegrees,
            float kickReturnPerSecond = DefaultKickReturnPerSecond,
            float maxKicks = DefaultMaxKicks,
            float adsSwayScale = DefaultAdsSwayScale,
            float adsKickScale = DefaultAdsKickScale)
        {
            BobHorizontalMeters = NonNegative(bobHorizontalMeters, nameof(bobHorizontalMeters));
            BobVerticalMeters = NonNegative(bobVerticalMeters, nameof(bobVerticalMeters));
            BobCyclesPerMeter = NonNegative(bobCyclesPerMeter, nameof(bobCyclesPerMeter));
            BobReferenceSpeed = Positive(bobReferenceSpeed, nameof(bobReferenceSpeed));
            BobSharpness = Positive(bobSharpness, nameof(bobSharpness));
            LookLagSeconds = NonNegative(lookLagSeconds, nameof(lookLagSeconds));
            MaxLookLagDegrees = NonNegative(maxLookLagDegrees, nameof(maxLookLagDegrees));
            LookLagSharpness = Positive(lookLagSharpness, nameof(lookLagSharpness));
            KickBackMeters = NonNegative(kickBackMeters, nameof(kickBackMeters));
            KickPitchDegrees = NonNegative(kickPitchDegrees, nameof(kickPitchDegrees));
            KickReturnPerSecond = Positive(kickReturnPerSecond, nameof(kickReturnPerSecond));
            MaxKicks = NonNegative(maxKicks, nameof(maxKicks));
            AdsSwayScale = Fraction(adsSwayScale, nameof(adsSwayScale));
            AdsKickScale = Fraction(adsKickScale, nameof(adsKickScale));
        }

        public float BobHorizontalMeters { get; }

        public float BobVerticalMeters { get; }

        public float BobCyclesPerMeter { get; }

        public float BobReferenceSpeed { get; }

        public float BobSharpness { get; }

        public float LookLagSeconds { get; }

        public float MaxLookLagDegrees { get; }

        public float LookLagSharpness { get; }

        public float KickBackMeters { get; }

        public float KickPitchDegrees { get; }

        public float KickReturnPerSecond { get; }

        // 連射で跳ね上がりが積み上がり、銃が画面の外へ出ないよう重ねる発数に上限を置く。
        public float MaxKicks { get; }

        public float AdsSwayScale { get; }

        public float AdsKickScale { get; }

        private static float NonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be a non-negative finite number");
            }

            return value;
        }

        private static float Positive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be a positive finite number");
            }

            return value;
        }

        private static float Fraction(float value, string name)
        {
            if (!(value >= 0f) || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be within [0, 1]");
            }

            return value;
        }
    }
}
