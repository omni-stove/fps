using System;

namespace Shooter.Core
{
    public sealed class TraversalSpec
    {
        public static readonly TraversalSpec Default = new TraversalSpec();

        public TraversalSpec(
            SlideTuning slide = null, ParachuteTuning parachute = null, ZiplineTuning zipline = null, SwimTuning swim = null, WallRunTuning wallRun = null)
        {
            Slide = slide ?? SlideTuning.Default;
            Parachute = parachute ?? ParachuteTuning.Default;
            Zipline = zipline ?? ZiplineTuning.Default;
            Swim = swim ?? SwimTuning.Default;
            WallRun = wallRun ?? WallRunTuning.Default;
        }

        public SlideTuning Slide { get; }

        public ParachuteTuning Parachute { get; }

        public ZiplineTuning Zipline { get; }

        public SwimTuning Swim { get; }

        public WallRunTuning WallRun { get; }
    }

    public sealed class SwimTuning
    {
        public const float DefaultSpeed = 3f;
        public const float DefaultBuoyancy = 12f;
        public const float DefaultBreathSeconds = 15f;
        public const int DefaultDrownDamage = 10;
        public const float DefaultDrownIntervalSeconds = 1f;

        public static readonly SwimTuning Default = new SwimTuning();

        public SwimTuning(
            float speed = DefaultSpeed,
            float buoyancy = DefaultBuoyancy,
            float breathSeconds = DefaultBreathSeconds,
            int drownDamage = DefaultDrownDamage,
            float drownIntervalSeconds = DefaultDrownIntervalSeconds)
        {
            TuningChecks.RequireNonNegativeFinite(speed, nameof(speed));
            TuningChecks.RequireNonNegativeFinite(buoyancy, nameof(buoyancy));
            TuningChecks.RequirePositiveFinite(breathSeconds, nameof(breathSeconds));
            if (drownDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(drownDamage), drownDamage, "drownDamage must not be negative");
            }

            TuningChecks.RequirePositiveFinite(drownIntervalSeconds, nameof(drownIntervalSeconds));
            Speed = speed;
            Buoyancy = buoyancy;
            BreathSeconds = breathSeconds;
            DrownDamage = drownDamage;
            DrownIntervalSeconds = drownIntervalSeconds;
        }

        public float Speed { get; }

        public float Buoyancy { get; }

        public float BreathSeconds { get; }

        public int DrownDamage { get; }

        public float DrownIntervalSeconds { get; }
    }

    public sealed class WallRunTuning
    {
        /// <summary>切なのは、壁走りが合わないゲームでも壁際で跳んだだけで走り出さないよう、使うゲームが入れるものにするため。</summary>
        public const bool DefaultEnabled = false;

        public const float DefaultMaxSeconds = 1.5f;
        public const float DefaultMinSpeed = 5f;
        public const float DefaultGravityScale = 0.25f;
        public const float DefaultJumpOffSpeed = 7f;

        public static readonly WallRunTuning Default = new WallRunTuning();

        public WallRunTuning(
            bool enabled = DefaultEnabled,
            float maxSeconds = DefaultMaxSeconds,
            float minSpeed = DefaultMinSpeed,
            float gravityScale = DefaultGravityScale,
            float jumpOffSpeed = DefaultJumpOffSpeed)
        {
            TuningChecks.RequirePositiveFinite(maxSeconds, nameof(maxSeconds));
            TuningChecks.RequireNonNegativeFinite(minSpeed, nameof(minSpeed));
            TuningChecks.RequireNonNegativeFinite(gravityScale, nameof(gravityScale));
            TuningChecks.RequireNonNegativeFinite(jumpOffSpeed, nameof(jumpOffSpeed));
            Enabled = enabled;
            MaxSeconds = maxSeconds;
            MinSpeed = minSpeed;
            GravityScale = gravityScale;
            JumpOffSpeed = jumpOffSpeed;
        }

        public bool Enabled { get; }

        public float MaxSeconds { get; }

        public float MinSpeed { get; }

        public float GravityScale { get; }

        public float JumpOffSpeed { get; }
    }

    internal static class TuningChecks
    {
        public static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }
        }

        public static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and positive");
            }
        }
    }

    public sealed class SlideTuning
    {
        public static readonly SlideTuning Default = new SlideTuning();

        public SlideTuning(
            float startSpeed = SlideMotion.DefaultStartSpeed,
            float deceleration = SlideMotion.DefaultDeceleration,
            float endSpeed = SlideMotion.DefaultEndSpeed,
            float minSeconds = SlideMotion.DefaultMinSeconds,
            float maxSeconds = SlideMotion.DefaultMaxSeconds,
            float slopeAcceleration = SlideMotion.DefaultSlopeAcceleration)
        {
            // 検査を SlideMotion と二重に持つと、片方だけ直したときに読めた値で滑れなくなる。
            _ = new SlideMotion(startSpeed, deceleration, endSpeed, minSeconds, maxSeconds, slopeAcceleration);
            StartSpeed = startSpeed;
            Deceleration = deceleration;
            EndSpeed = endSpeed;
            MinSeconds = minSeconds;
            MaxSeconds = maxSeconds;
            SlopeAcceleration = slopeAcceleration;
        }

        public float StartSpeed { get; }

        public float Deceleration { get; }

        public float EndSpeed { get; }

        public float MinSeconds { get; }

        public float MaxSeconds { get; }

        public float SlopeAcceleration { get; }
    }

    public sealed class ParachuteTuning
    {
        public static readonly ParachuteTuning Default = new ParachuteTuning();

        public ParachuteTuning(
            float minOpenHeight = ParachuteMotion.DefaultMinOpenHeight,
            float descentSpeed = ParachuteMotion.DefaultDescentSpeed,
            float horizontalSpeed = ParachuteMotion.DefaultHorizontalSpeed,
            float steerAcceleration = ParachuteMotion.DefaultSteerAcceleration)
        {
            // 検査を ParachuteMotion と二重に持つと、片方だけ直したときに読めた値で開けなくなる。
            _ = new ParachuteMotion(minOpenHeight, descentSpeed, horizontalSpeed, steerAcceleration);
            MinOpenHeight = minOpenHeight;
            DescentSpeed = descentSpeed;
            HorizontalSpeed = horizontalSpeed;
            SteerAcceleration = steerAcceleration;
        }

        public float MinOpenHeight { get; }

        public float DescentSpeed { get; }

        public float HorizontalSpeed { get; }

        public float SteerAcceleration { get; }
    }

    public sealed class ZiplineTuning
    {
        public const float DefaultGrabRadius = 1.5f;
        public const float DefaultHangOffset = 1.3f;
        public const float DefaultRegrabSeconds = 0.5f;
        public const float DefaultSpreadMoveFraction = 1f;

        public static readonly ZiplineTuning Default = new ZiplineTuning();

        public ZiplineTuning(
            float grabRadius = DefaultGrabRadius,
            float hangOffset = DefaultHangOffset,
            float regrabSeconds = DefaultRegrabSeconds,
            float spreadMoveFraction = DefaultSpreadMoveFraction)
        {
            RequireNonNegativeFinite(grabRadius, nameof(grabRadius));
            RequireNonNegativeFinite(hangOffset, nameof(hangOffset));
            RequireNonNegativeFinite(regrabSeconds, nameof(regrabSeconds));

            // ZiplineMovement は 1 を超える値を 1 に丸めるが、データで書いた値が黙って変わると書き手が気付けない。
            if (!(spreadMoveFraction >= 0f && spreadMoveFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(spreadMoveFraction), spreadMoveFraction, "spreadMoveFraction must be between 0 and 1");
            }

            GrabRadius = grabRadius;
            HangOffset = hangOffset;
            RegrabSeconds = regrabSeconds;
            SpreadMoveFraction = spreadMoveFraction;
        }

        public float GrabRadius { get; }

        public float HangOffset { get; }

        public float RegrabSeconds { get; }

        public float SpreadMoveFraction { get; }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }
        }
    }
}
