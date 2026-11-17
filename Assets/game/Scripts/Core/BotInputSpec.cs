using System;

namespace Shooter.Core
{
    // 人と同じ移動・射撃を通すので、強さは旋回の速さと照準の誤差で人並みに抑える。
    public sealed class BotInputSpec
    {
        // マウスで素早く振り向く人よりは遅くする。
        public const float DefaultMaxTurnDegreesPerSecond = 240f;

        public const float DefaultAimErrorPerMeter = 0.03f;

        public const float DefaultAimErrorRefreshSeconds = 0.5f;

        public const float DefaultFireToleranceDegrees = 3f;

        public const float DefaultSprintDistance = 8f;

        public const float DefaultSprintMaxAngleDegrees = 30f;

        public static BotInputSpec Default { get; } = new BotInputSpec(
            DefaultMaxTurnDegreesPerSecond,
            DefaultAimErrorPerMeter,
            DefaultAimErrorRefreshSeconds,
            DefaultFireToleranceDegrees,
            EnemySpec.DefaultArrivalDistance,
            DefaultSprintDistance,
            DefaultSprintMaxAngleDegrees,
            false);

        public float MaxTurnDegreesPerSecond { get; }

        public float AimErrorPerMeter { get; }

        public float AimErrorRefreshSeconds { get; }

        public float FireToleranceDegrees { get; }

        public float ArrivalDistance { get; }

        public float SprintDistance { get; }

        public float SprintMaxAngleDegrees { get; }

        public bool CrouchWhenHolding { get; }

        public BotInputSpec(
            float maxTurnDegreesPerSecond,
            float aimErrorPerMeter,
            float aimErrorRefreshSeconds,
            float fireToleranceDegrees,
            float arrivalDistance,
            float sprintDistance,
            float sprintMaxAngleDegrees,
            bool crouchWhenHolding)
        {
            if (!(maxTurnDegreesPerSecond > 0f) || float.IsInfinity(maxTurnDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTurnDegreesPerSecond), maxTurnDegreesPerSecond, "maxTurnDegreesPerSecond must be positive and finite");
            }

            if (!(aimErrorPerMeter >= 0f && aimErrorPerMeter <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(aimErrorPerMeter), aimErrorPerMeter, "aimErrorPerMeter must be in [0, 1]");
            }

            if (!(aimErrorRefreshSeconds > 0f) || float.IsInfinity(aimErrorRefreshSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aimErrorRefreshSeconds), aimErrorRefreshSeconds, "aimErrorRefreshSeconds must be positive and finite");
            }

            if (!(fireToleranceDegrees > 0f && fireToleranceDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fireToleranceDegrees), fireToleranceDegrees, "fireToleranceDegrees must be in (0, 180]");
            }

            if (!(arrivalDistance > 0f) || float.IsInfinity(arrivalDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(arrivalDistance), arrivalDistance, "arrivalDistance must be positive and finite");
            }

            if (!(sprintDistance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(sprintDistance), sprintDistance, "sprintDistance must not be negative");
            }

            if (!(sprintMaxAngleDegrees >= 0f && sprintMaxAngleDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sprintMaxAngleDegrees), sprintMaxAngleDegrees, "sprintMaxAngleDegrees must be in [0, 90]");
            }

            MaxTurnDegreesPerSecond = maxTurnDegreesPerSecond;
            AimErrorPerMeter = aimErrorPerMeter;
            AimErrorRefreshSeconds = aimErrorRefreshSeconds;
            FireToleranceDegrees = fireToleranceDegrees;
            ArrivalDistance = arrivalDistance;
            SprintDistance = sprintDistance;
            SprintMaxAngleDegrees = sprintMaxAngleDegrees;
            CrouchWhenHolding = crouchWhenHolding;
        }
    }
}
