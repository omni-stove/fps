using System;

namespace Shooter.Core
{
    public sealed class ExplosivePropSpec
    {
        // rifle の 1 発 (25) で爆発するようにする。
        public const int DefaultMaxHealth = 20;

        // グレネード (GrenadeSpec.DefaultMaxDamage) とそろえる。
        public const int DefaultMaxDamage = 150;

        public const float DefaultDamageRadius = 5f;

        public const float DefaultFullDamageRadius = 1.5f;

        public const int DefaultStructureDamageAtCenter = 150;

        public const float DefaultChainDelaySeconds = 0.3f;

        public const float DefaultBurnSeconds = 0f;

        public static readonly ExplosivePropSpec Default = new ExplosivePropSpec();

        public ExplosivePropSpec(
            int maxHealth = DefaultMaxHealth,
            int maxDamage = DefaultMaxDamage,
            float damageRadius = DefaultDamageRadius,
            float fullDamageRadius = DefaultFullDamageRadius,
            int structureDamageAtCenter = DefaultStructureDamageAtCenter,
            float chainDelaySeconds = DefaultChainDelaySeconds,
            float burnSeconds = DefaultBurnSeconds)
        {
            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            if (!(damageRadius > 0f) || float.IsInfinity(damageRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(damageRadius), damageRadius, "damageRadius must be positive and finite");
            }

            // 同じ値を許すと、減衰の幅が 0 になり ExplosionDamage が距離で割れない。
            if (!(fullDamageRadius >= 0f) || fullDamageRadius >= damageRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullDamageRadius), fullDamageRadius, "fullDamageRadius must be non-negative and less than damageRadius");
            }

            if (structureDamageAtCenter < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(structureDamageAtCenter), structureDamageAtCenter, "structureDamageAtCenter must not be negative");
            }

            RequireNonNegativeFinite(chainDelaySeconds, nameof(chainDelaySeconds));
            RequireNonNegativeFinite(burnSeconds, nameof(burnSeconds));

            MaxHealth = maxHealth;
            StructureDamageAtCenter = structureDamageAtCenter;
            ChainDelaySeconds = chainDelaySeconds;
            BurnSeconds = burnSeconds;
            Explosion = new ExplosionDamage(maxDamage, damageRadius, fullDamageRadius);
        }

        public int MaxHealth { get; }

        public int MaxDamage => Explosion.MaxDamage;

        public float DamageRadius => Explosion.Radius;

        public float FullDamageRadius => Explosion.FullDamageRadius;

        public int StructureDamageAtCenter { get; }

        public float ChainDelaySeconds { get; }

        public float BurnSeconds { get; }

        public bool Burns => BurnSeconds > 0f;

        public ExplosionDamage Explosion { get; }

        public float DetonationDelay(bool chained)
        {
            return chained ? Math.Max(BurnSeconds, ChainDelaySeconds) : BurnSeconds;
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
