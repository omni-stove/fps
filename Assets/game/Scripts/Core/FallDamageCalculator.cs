using System;

namespace Shooter.Core
{
    // オフラインとサーバーで落下ダメージがずれないよう、どちらもこの式を使う。
    public static class FallDamageCalculator
    {
        public static int Calculate(float fallSpeed, MotorSpec spec, int maxHealth)
        {
            if (!(fallSpeed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed), fallSpeed, "fallSpeed must not be negative or NaN");
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (fallSpeed <= spec.SafeFallSpeed)
            {
                return 0;
            }

            if (fallSpeed >= spec.LethalFallSpeed)
            {
                return maxHealth;
            }

            // 切り捨てにするのは、四捨五入だと LethalFallSpeed の手前でも満タンから倒れる値になり、
            // 「LethalFallSpeed 以上で倒れる」という閾値の意味が崩れるため。
            var t = (fallSpeed - (double)spec.SafeFallSpeed) / ((double)spec.LethalFallSpeed - spec.SafeFallSpeed);
            var damage = (int)Math.Floor(maxHealth * t);
            return Math.Min(Math.Max(damage, 0), maxHealth - 1);
        }

        // 水の中でも引数を確かめてから 0 を返すのは、水があるかで壊れた値を見逃すかが変わらないようにするため。
        public static int Calculate(float fallSpeed, MotorSpec spec, int maxHealth, bool intoWater)
        {
            var damage = Calculate(fallSpeed, spec, maxHealth);
            return intoWater ? 0 : damage;
        }
    }
}
