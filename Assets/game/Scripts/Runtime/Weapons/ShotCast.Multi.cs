using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ShotCast
    {
        // 厚さを測る線を当てた面の向こうから戻すとき、入った面そのものに当たって厚さ 0 と測らないよう手前で止める幅。
        private const float ThicknessSkin = 0.01f;

        private static readonly Comparer<RaycastHit> _byDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        public static int CastAll(
            Vector3 origin, Vector3 direction, float maxDistance, int layerMask, Transform shooterRoot, List<RaycastHit> hits)
        {
            if (hits == null)
            {
                throw new ArgumentNullException(nameof(hits));
            }

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

            var start = hits.Count;
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

                hits.Add(candidate);
            }

            Array.Clear(_buffer, 0, count);
            hits.Sort(start, hits.Count - start, _byDistance);
            return hits.Count - start;
        }

        public static float Thickness(Collider collider, Vector3 point, Vector3 direction, float maxThickness)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            if (direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(maxThickness >= 0f) || float.IsInfinity(maxThickness))
            {
                throw new ArgumentOutOfRangeException(nameof(maxThickness), maxThickness, "maxThickness must be a non-negative finite number");
            }

            // 向こう側から戻る線が出口の面に当たれば、その手前までが厚さ。始点が形の中なら当たらず、厚すぎることになる。
            var unit = direction.normalized;
            var probe = maxThickness + ThicknessSkin;
            var back = new Ray(point + (unit * probe), -unit);
            if (!collider.Raycast(back, out var exit, probe - (ThicknessSkin * 0.5f)))
            {
                return float.PositiveInfinity;
            }

            return Mathf.Max(0f, probe - exit.distance);
        }
    }
}
