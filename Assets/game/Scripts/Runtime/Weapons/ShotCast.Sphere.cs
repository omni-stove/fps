using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ShotCast
    {
        public static bool TryFirst(
            Vector3 origin, Vector3 direction, float radius, float maxDistance, int layerMask, Transform shooterRoot, out RaycastHit hit)
        {
            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            // 球の判定は始めから重なっている物を拾わないので、細い線で真正面の壁を先に押さえる (サーバーの ServerHitboxes.CastSphere と同じ)。
            var found = TryFirst(origin, direction, maxDistance, layerMask, shooterRoot, out hit);
            var limit = found ? hit.distance : maxDistance;
            if (radius == 0f || !(limit > 0f))
            {
                return found;
            }

            if (TryFirstSphere(origin, direction.normalized, radius, limit, layerMask, shooterRoot, out var sphere))
            {
                hit = sphere;
                return true;
            }

            return found;
        }

        private static bool TryFirstSphere(
            Vector3 origin, Vector3 direction, float radius, float maxDistance, int layerMask, Transform shooterRoot, out RaycastHit hit)
        {
            var count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _buffer,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);

            hit = default;
            var found = false;
            var shooter = shooterRoot != null ? shooterRoot.gameObject : null;
            for (var i = 0; i < count; i++)
            {
                var candidate = _buffer[i];

                // 始めから重なっている物は距離 0・位置が原点で返る。銃口の横をかすめた壁で弾が止まらないよう外す。
                if (candidate.distance <= 0f && candidate.point == Vector3.zero)
                {
                    continue;
                }

                if (shooterRoot != null && candidate.collider.transform.IsChildOf(shooterRoot))
                {
                    continue;
                }

                if (shooter != null && Deployable.LetsThrough(candidate.collider, shooter))
                {
                    continue;
                }

                if (!found || candidate.distance < hit.distance)
                {
                    hit = candidate;
                    found = true;
                }
            }

            Array.Clear(_buffer, 0, count);
            return found;
        }
    }
}
