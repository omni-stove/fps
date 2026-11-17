using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ShotCast
    {
        // 当たった点から撃ち直すと、浮動小数の誤差で跳ねた面そのものにもう一度当たることがあるので、面から少し離して撃つ。
        public const float RicochetSurfaceOffset = 0.01f;

        public static bool TryBounce(
            RicochetSpec ricochet, int bounceCount, Vector3 direction, RaycastHit surface, float remainingRange,
            out Vector3 bounced, out float damageMultiplier)
        {
            bounced = direction;
            damageMultiplier = 1f;
            if (ricochet == null || !(remainingRange > RicochetSurfaceOffset) || surface.collider == null)
            {
                return false;
            }

            // 人・盾・置いた物に当たった弾はそこで止まり、受け止めた側が処理する。跳ねるのは世界の面だけ。
            var collider = surface.collider;
            if (Hitbox.FindFor(collider) != null
                || collider.GetComponentInParent<Damageable>() != null
                || collider.GetComponent<BulletShield>() != null
                || Deployable.Of(collider) != null)
            {
                return false;
            }

            if (!RicochetRule.TryBounce(
                    ricochet, bounceCount, direction.ToNumerics(), surface.normal.ToNumerics(), out var next, out damageMultiplier))
            {
                return false;
            }

            bounced = next.ToUnity();
            return true;
        }

        public static bool TryFirstAfterBounce(
            RaycastHit surface, Vector3 bounced, float radius, float remainingRange, int layerMask, Transform shooterRoot, out RaycastHit hit)
        {
            var origin = surface.point + (surface.normal.normalized * RicochetSurfaceOffset);
            return TryFirst(origin, bounced, radius, remainingRange - RicochetSurfaceOffset, layerMask, shooterRoot, out hit);
        }
    }
}
