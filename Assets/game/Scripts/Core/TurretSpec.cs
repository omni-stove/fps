using System;

namespace Fps.Core
{
    public sealed class TurretSpec
    {
        public const int DefaultRounds = 60;

        public const float DefaultLifetimeSeconds = 30f;

        // 射撃場の他の敵 (視界の外に置いてある) を見つけない距離に収める。
        public const float DefaultSightRange = 12f;

        public const float DefaultTurnDegreesPerSecond = 270f;

        public const float DefaultAimToleranceDegrees = 5f;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形なので、タレットの形と数はここに分けて持つ。
        public const int DefaultMaxActive = 1;

        public const int DefaultMaxHealth = 150;

        public const float DefaultWidth = 0.8f;

        public const float DefaultHeight = 1.2f;

        public const float DefaultDepth = 0.8f;

        public static TurretSpec Default { get; } = new TurretSpec(
            DefaultRounds,
            DefaultLifetimeSeconds,
            DefaultSightRange,
            DefaultTurnDegreesPerSecond,
            DefaultAimToleranceDegrees);

        public TurretSpec(int rounds, float lifetimeSeconds, float sightRange, float turnDegreesPerSecond, float aimToleranceDegrees)
        {
            if (rounds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds), rounds, "rounds must be at least 1");
            }

            RequirePositive(lifetimeSeconds, nameof(lifetimeSeconds));
            RequirePositive(sightRange, nameof(sightRange));
            RequirePositive(turnDegreesPerSecond, nameof(turnDegreesPerSecond));
            if (!(aimToleranceDegrees >= 0f) || aimToleranceDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(aimToleranceDegrees), aimToleranceDegrees, "aimToleranceDegrees must be between 0 and 180");
            }

            Rounds = rounds;
            LifetimeSeconds = lifetimeSeconds;
            SightRange = sightRange;
            TurnDegreesPerSecond = turnDegreesPerSecond;
            AimToleranceDegrees = aimToleranceDegrees;
        }

        public int Rounds { get; }

        public float LifetimeSeconds { get; }

        public float SightRange { get; }

        public float TurnDegreesPerSecond { get; }

        public float AimToleranceDegrees { get; }

        private static void RequirePositive(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive and finite");
            }
        }
    }
}
