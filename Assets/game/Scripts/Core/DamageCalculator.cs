using System;

namespace Shooter.Core
{
    public static class DamageCalculator
    {
        public const double HeadMultiplier = 2.0;

        public const double BodyMultiplier = 1.0;

        public const double LimbMultiplier = 0.75;

        public const double WeakPointMultiplier = 2.0;

        public static double ZoneMultiplier(HitZone zone)
        {
            return ZoneDamageMultipliers.Default.For(zone);
        }

        public static int Calculate(int baseDamage, HitZone zone, float distance, DamageFalloff falloff)
        {
            return Calculate(baseDamage, zone, distance, falloff, ZoneDamageMultipliers.Default);
        }

        public static int Calculate(int baseDamage, HitZone zone, float distance, DamageFalloff falloff, ZoneDamageMultipliers multipliers)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            if (falloff == null)
            {
                throw new ArgumentNullException(nameof(falloff));
            }

            if (multipliers == null)
            {
                throw new ArgumentNullException(nameof(multipliers));
            }

            var zoneMultiplier = multipliers.For(zone);
            var distanceMultiplier = falloff.MultiplierAt(distance);

            // 倍率 0 の部位を効かない部位として使えるよう、最低 1 の決まりより先に 0 を返す。
            if (baseDamage == 0 || zoneMultiplier == 0d)
            {
                return 0;
            }

            var rounded = Math.Round(baseDamage * zoneMultiplier * distanceMultiplier, MidpointRounding.AwayFromZero);
            if (rounded >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)rounded);
        }

        public static int Scale(int damage, float multiplier)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!(multiplier >= 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must not be negative and must be finite");
            }

            // Calculate と同じく、倍率 0 を効かない部位として使えるよう最低 1 の決まりより先に 0 を返す。
            if (damage == 0 || multiplier == 0f)
            {
                return 0;
            }

            var rounded = Math.Round(damage * (double)multiplier, MidpointRounding.AwayFromZero);
            if (rounded >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)rounded);
        }
    }
}
