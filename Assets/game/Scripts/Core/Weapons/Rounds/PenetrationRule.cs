using System;

namespace Fps.Core
{
    public static class PenetrationRule
    {
        // 太い弾は最初の当たりで止まる (ServerHitboxes.CastSphere が 1 件しか拾わない) ので、貫通は細い弾だけに効かせ、オフラインもそれに揃える。
        public static PenetrationSpec For(RoundSpec round)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            return round.Radius > 0f ? null : round.Penetration;
        }

        // 厚さを測る側が、これより厚ければ測り切らずに諦めてよい長さ。
        public static float MaxThicknessFor(PenetrationSpec penetration, string surfaceMaterialId)
        {
            if (penetration == null)
            {
                return 0f;
            }

            var max = penetration.MaxThickness * penetration.PenetrabilityOf(surfaceMaterialId);
            return float.IsInfinity(max) ? float.MaxValue : max;
        }

        // damageMultiplier はこの 1 枚を抜けた分の倍率で、それまでに抜けた分は呼ぶ側が掛け合わせる。
        // 測り切れなかった厚さは float.PositiveInfinity で渡す。
        public static bool TryPassThrough(
            PenetrationSpec penetration, int passedCount, string surfaceMaterialId, float thickness, out float damageMultiplier)
        {
            if (passedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(passedCount), passedCount, "passedCount must not be negative");
            }

            if (!(thickness >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "thickness must not be negative or NaN");
            }

            damageMultiplier = 1f;
            if (penetration == null || passedCount >= penetration.MaxPenetrations || float.IsPositiveInfinity(thickness))
            {
                return false;
            }

            var penetrability = penetration.PenetrabilityOf(surfaceMaterialId);
            if (!(penetrability > 0f) || thickness > MaxThicknessFor(penetration, surfaceMaterialId))
            {
                return false;
            }

            damageMultiplier = penetration.DamageMultiplierPerPenetration;
            return true;
        }
    }
}
