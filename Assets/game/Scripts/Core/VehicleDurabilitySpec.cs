using System;

namespace Shooter.Core
{
    public sealed class VehicleDurabilitySpec
    {
        public const float DefaultSmokeHealthFraction = 0.5f;
        public const float DefaultBurnHealthFraction = 0.25f;
        public const float DefaultBurnDamagePerSecond = 25f;
        public const int DefaultExplosionMaxDamage = 150;
        public const float DefaultExplosionRadius = 8f;
        public const float DefaultExplosionFullDamageRadius = 2f;
        public const int DefaultExplosionStructureDamage = 150;

        public static readonly VehicleDurabilitySpec Default = new VehicleDurabilitySpec();

        public VehicleDurabilitySpec(
            float smokeHealthFraction = DefaultSmokeHealthFraction,
            float burnHealthFraction = DefaultBurnHealthFraction,
            float burnDamagePerSecond = DefaultBurnDamagePerSecond,
            ExplosionSpec explosion = null)
        {
            if (!(smokeHealthFraction >= 0f && smokeHealthFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(smokeHealthFraction), smokeHealthFraction, "smokeHealthFraction must be between 0 and 1");
            }

            if (!(burnHealthFraction >= 0f && burnHealthFraction <= smokeHealthFraction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burnHealthFraction), burnHealthFraction, "burnHealthFraction must be between 0 and smokeHealthFraction");
            }

            if (!(burnDamagePerSecond >= 0f) || float.IsInfinity(burnDamagePerSecond))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burnDamagePerSecond), burnDamagePerSecond, "burnDamagePerSecond must not be negative and must be finite");
            }

            SmokeHealthFraction = smokeHealthFraction;
            BurnHealthFraction = burnHealthFraction;
            BurnDamagePerSecond = burnDamagePerSecond;
            Explosion = explosion ?? new ExplosionSpec(
                new ExplosionDamage(DefaultExplosionMaxDamage, DefaultExplosionRadius, DefaultExplosionFullDamageRadius),
                DefaultExplosionStructureDamage);
        }

        public float SmokeHealthFraction { get; }

        public float BurnHealthFraction { get; }

        public float BurnDamagePerSecond { get; }

        public ExplosionSpec Explosion { get; }

        public VehicleDamageStage StageFor(int health, int maxHealth)
        {
            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            if (health <= 0)
            {
                return VehicleDamageStage.Destroyed;
            }

            // 境目ちょうどを傷んだ側に入れるのは、割合 0.5 の煙が耐久 1000 の 500 で出るように、設定した数がそのまま境目になるようにするため。
            if (health <= (double)maxHealth * BurnHealthFraction)
            {
                return VehicleDamageStage.Burning;
            }

            return health <= (double)maxHealth * SmokeHealthFraction ? VehicleDamageStage.Smoking : VehicleDamageStage.Intact;
        }
    }
}
