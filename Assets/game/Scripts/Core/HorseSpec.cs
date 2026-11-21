using System;

namespace Shooter.Core
{
    public sealed class HorseSpec
    {
        public const float DefaultMass = 500f;
        public const float DefaultWalkSpeed = 1.8f;
        public const float DefaultTrotSpeed = 4.5f;
        public const float DefaultCanterSpeed = 9f;
        public const float DefaultReverseSpeed = 1.2f;
        public const float DefaultAcceleration = 4f;
        public const float DefaultDeceleration = 8f;
        public const float DefaultTurnRate = 120f;
        public const float DefaultCanterTurnScale = 0.35f;
        public const float DefaultJumpSpeed = 5f;
        public const float DefaultMaxSlopeAngle = 40f;
        public const float DefaultWalkThreshold = 0.2f;
        public const float DefaultTrotThreshold = 2.5f;
        public const float DefaultCanterThreshold = 6f;

        public const float MaxSlopeAngleLimit = 90f;

        public HorseSpec(
            float mass = DefaultMass,
            float walkSpeed = DefaultWalkSpeed,
            float trotSpeed = DefaultTrotSpeed,
            float canterSpeed = DefaultCanterSpeed,
            float reverseSpeed = DefaultReverseSpeed,
            float acceleration = DefaultAcceleration,
            float deceleration = DefaultDeceleration,
            float turnRate = DefaultTurnRate,
            float canterTurnScale = DefaultCanterTurnScale,
            float jumpSpeed = DefaultJumpSpeed,
            float maxSlopeAngle = DefaultMaxSlopeAngle,
            float walkThreshold = DefaultWalkThreshold,
            float trotThreshold = DefaultTrotThreshold,
            float canterThreshold = DefaultCanterThreshold)
        {
            RequirePositive(mass, nameof(mass));
            RequirePositive(walkSpeed, nameof(walkSpeed));
            RequirePositive(trotSpeed, nameof(trotSpeed));
            RequirePositive(canterSpeed, nameof(canterSpeed));
            // 段階が上がるほど速くないと、スロットルを強めたり Gallop を押したりして遅くなるため。
            if (trotSpeed < walkSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(trotSpeed), trotSpeed, $"trotSpeed must not be less than walkSpeed {walkSpeed}");
            }

            if (canterSpeed < trotSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(canterSpeed), canterSpeed, $"canterSpeed must not be less than trotSpeed {trotSpeed}");
            }

            RequireNonNegative(reverseSpeed, nameof(reverseSpeed));
            if (reverseSpeed > walkSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(reverseSpeed), reverseSpeed, $"reverseSpeed must not exceed walkSpeed {walkSpeed}");
            }

            RequirePositive(acceleration, nameof(acceleration));
            RequirePositive(deceleration, nameof(deceleration));
            RequireNonNegative(turnRate, nameof(turnRate));
            if (!(canterTurnScale >= 0f && canterTurnScale <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(canterTurnScale), canterTurnScale, "canterTurnScale must be between 0 and 1");
            }

            RequireNonNegative(jumpSpeed, nameof(jumpSpeed));
            if (!(maxSlopeAngle > 0f && maxSlopeAngle < MaxSlopeAngleLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlopeAngle), maxSlopeAngle, "maxSlopeAngle must be greater than 0 and less than 90");
            }

            RequirePositive(walkThreshold, nameof(walkThreshold));
            if (!(trotThreshold > walkThreshold) || float.IsInfinity(trotThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(trotThreshold), trotThreshold, $"trotThreshold must be finite and greater than walkThreshold {walkThreshold}");
            }

            if (!(canterThreshold > trotThreshold) || float.IsInfinity(canterThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(canterThreshold), canterThreshold, $"canterThreshold must be finite and greater than trotThreshold {trotThreshold}");
            }

            // 境目がその段階の最高速度より上だと、その段階の入力では届かず、歩き方が上がらないため。
            if (walkThreshold > walkSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(walkThreshold), walkThreshold, $"walkThreshold must not exceed walkSpeed {walkSpeed}");
            }

            if (trotThreshold > trotSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(trotThreshold), trotThreshold, $"trotThreshold must not exceed trotSpeed {trotSpeed}");
            }

            if (canterThreshold > canterSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(canterThreshold), canterThreshold, $"canterThreshold must not exceed canterSpeed {canterSpeed}");
            }

            Mass = mass;
            WalkSpeed = walkSpeed;
            TrotSpeed = trotSpeed;
            CanterSpeed = canterSpeed;
            ReverseSpeed = reverseSpeed;
            Acceleration = acceleration;
            Deceleration = deceleration;
            TurnRate = turnRate;
            CanterTurnScale = canterTurnScale;
            JumpSpeed = jumpSpeed;
            MaxSlopeAngle = maxSlopeAngle;
            WalkThreshold = walkThreshold;
            TrotThreshold = trotThreshold;
            CanterThreshold = canterThreshold;
        }

        public static HorseSpec Default => new HorseSpec();

        public float Mass { get; }

        public float WalkSpeed { get; }

        public float TrotSpeed { get; }

        public float CanterSpeed { get; }

        public float ReverseSpeed { get; }

        public float Acceleration { get; }

        public float Deceleration { get; }

        public float TurnRate { get; }

        public float CanterTurnScale { get; }

        public float JumpSpeed { get; }

        public float MaxSlopeAngle { get; }

        public float WalkThreshold { get; }

        public float TrotThreshold { get; }

        public float CanterThreshold { get; }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
