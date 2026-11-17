using System;

namespace Shooter.Core
{
    public static class ChargeScaling
    {
        public static float DamageMultiplier(ChargeSpec charge, float chargeFraction)
        {
            return charge == null ? 1f : Lerp(charge.FullChargeDamageMultiplier, Effective(charge, chargeFraction));
        }

        public static float SpeedMultiplier(ChargeSpec charge, float chargeFraction)
        {
            return charge == null ? 1f : Lerp(charge.FullChargeSpeedMultiplier, Effective(charge, chargeFraction));
        }

        // 最小に届かずに撃てた弾 (FiresBelowMin) は最小の溜めの威力にする。届かない弾を撃たない武器では、その値が届くのは不正な要求だけ。
        private static float Effective(ChargeSpec charge, float chargeFraction)
        {
            if (float.IsNaN(chargeFraction))
            {
                throw new ArgumentOutOfRangeException(nameof(chargeFraction), chargeFraction, "chargeFraction must be a number");
            }

            return Math.Max(charge.MinChargeFraction, Math.Min(1f, chargeFraction));
        }

        private static float Lerp(float full, float fraction)
        {
            return 1f + ((full - 1f) * fraction);
        }
    }
}
