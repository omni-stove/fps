using System;

namespace Shooter.Core
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

        public static float MaxThicknessFor(PenetrationSpec penetration, string surfaceMaterialId)
        {
            if (penetration == null)
            {
                return 0f;
            }

            var max = penetration.MaxThickness * penetration.PenetrabilityOf(surfaceMaterialId);
            return float.IsInfinity(max) ? float.MaxValue : max;
        }

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
