using System;

namespace Shooter.Core
{
    public sealed class ExplosionDamage
    {
        public ExplosionDamage(int maxDamage, float radius, float fullDamageRadius = 0f)
        {
            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(fullDamageRadius >= 0f) || fullDamageRadius >= radius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullDamageRadius), fullDamageRadius, "fullDamageRadius must be non-negative and less than radius");
            }

            MaxDamage = maxDamage;
            Radius = radius;
            FullDamageRadius = fullDamageRadius;
        }

        public int MaxDamage { get; }

        public float Radius { get; }

        public float FullDamageRadius { get; }

        public bool Reaches(float distance)
        {
            RequireDistance(distance);
            return distance < Radius;
        }

        // 遮るかの判定 (線を引く) は物理を持つ呼ぶ側に任せ、結果だけを obstructed で受け取る。
        public int At(float distance, bool obstructed)
        {
            if (!Reaches(distance) || obstructed)
            {
                return 0;
            }

            if (distance <= FullDamageRadius)
            {
                return MaxDamage;
            }

            var t = (distance - (double)FullDamageRadius) / ((double)Radius - FullDamageRadius);
            var damage = (int)Math.Round(MaxDamage * (1d - t), MidpointRounding.AwayFromZero);
            return Math.Min(Math.Max(damage, 0), MaxDamage);
        }

        // At から逆算しないのは、整数に丸めたダメージでは MaxDamage が小さいときに強さが段になるため。
        public float StrengthAt(float distance)
        {
            if (!Reaches(distance))
            {
                return 0f;
            }

            if (distance <= FullDamageRadius)
            {
                return 1f;
            }

            var t = (distance - (double)FullDamageRadius) / ((double)Radius - FullDamageRadius);
            return (float)Math.Min(Math.Max(1d - t, 0d), 1d);
        }

        private static void RequireDistance(float distance)
        {
            if (!(distance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be negative or NaN");
            }
        }
    }
}
