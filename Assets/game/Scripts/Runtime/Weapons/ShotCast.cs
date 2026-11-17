using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ShotCast
    {
        // これを超える数のコライダーが一直線に並ぶと遠いものを取りこぼす。毎発の確保を避けるため上限を決めて使い回す。
        public const int MaxHits = 32;

        private static readonly RaycastHit[] _buffer = new RaycastHit[MaxHits];

        public static bool TryFirst(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, Transform shooterRoot, out RaycastHit hit)
        {
            if (direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            var count = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
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

            // 消えたコライダーへの参照を次の弾まで持ち続けないよう空ける。
            Array.Clear(_buffer, 0, count);
            return found;
        }
    }
}
