using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が建造物 (<see cref="IStructureHittable"/>) に当たったときの被害の配り方。
    /// 半径 0 なら当たった物だけに、半径があれば着弾点を中心とした球の中の部品すべてに、距離で減らして渡す。
    /// </summary>
    public static class StructureDamage
    {
        /// <summary>
        /// hitCollider への命中を建造物に渡し、<see cref="IStructureHittable.ApplyStructureHit"/> を呼んだ回数を返す。
        /// radius が 0 なら hitCollider の親をたどって見つけた <see cref="IStructureHittable"/> に damage をそのまま 1 回渡す
        /// (見つからなければ 0 回)。
        /// radius が 0 より大きければ、point を中心に radius の球に触れるコライダー (トリガーを除く) を集め、
        /// それぞれの親の <see cref="IStructureHittable"/> に、コライダーの point に最も近い点までの距離に比例して減らした被害
        /// (中心で damage、縁で 0、整数に丸める) を渡す。1 つの建造物でも部品 (コライダー) ごとに呼ぶ。
        /// </summary>
        /// <param name="hitCollider">弾が当たったコライダー。null 不可。</param>
        /// <param name="point">着弾点。</param>
        /// <param name="damage">中心での被害。0 以上。</param>
        /// <param name="radius">被害を広げる半径 (m)。0 以上の有限値。</param>
        public static int Apply(Collider hitCollider, Vector3 point, int damage, float radius)
        {
            if (hitCollider == null)
            {
                throw new ArgumentNullException(nameof(hitCollider));
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            if (radius == 0f)
            {
                var hittable = FindHittable(hitCollider);
                if (hittable == null)
                {
                    return 0;
                }

                hittable.ApplyStructureHit(hitCollider, point, damage);
                return 1;
            }

            var colliders = Physics.OverlapSphere(point, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // 子コライダーを持つ部品が、コライダーの数だけ被害を重ねて受けないよう、部品ごとに最大の被害を 1 回だけ渡す。
            var entries = new System.Collections.Generic.List<(IStructureHittable hittable, Collider collider, int damage)>();
            var indexByKey = new System.Collections.Generic.Dictionary<(IStructureHittable, Object), int>();
            foreach (var collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                var hittable = FindHittable(collider);
                if (hittable == null)
                {
                    continue;
                }

                var distance = Vector3.Distance(point, ClosestPoint(collider, point));
                var scaled = Scale(damage, distance, radius);
                var piece = collider.GetComponentInParent<DestructiblePiece>();
                var key = (hittable, piece != null ? (Object)piece : collider);
                if (indexByKey.TryGetValue(key, out var existing))
                {
                    if (scaled > entries[existing].damage)
                    {
                        entries[existing] = (hittable, collider, scaled);
                    }
                }
                else
                {
                    indexByKey[key] = entries.Count;
                    entries.Add((hittable, collider, scaled));
                }
            }

            var count = 0;
            foreach (var entry in entries)
            {
                // 前の部品への被害で、この部品が DestroyImmediate されていることがある。
                if (entry.collider == null)
                {
                    continue;
                }

                entry.hittable.ApplyStructureHit(entry.collider, point, entry.damage);
                count++;
            }

            return count;
        }

        /// <summary>中心からの距離 distance での被害。中心で damage、radius 以上で 0。</summary>
        public static int Scale(int damage, float distance, float radius)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            var ratio = 1f - Mathf.Clamp01(distance / radius);
            return Mathf.Clamp(Mathf.RoundToInt(damage * ratio), 0, damage);
        }

        private static IStructureHittable FindHittable(Collider collider)
        {
            var hittable = collider.GetComponentInParent<IStructureHittable>();

            // 破棄済みの Component は C# の null ではないが、Unity の == では null になる。
            if (hittable is Object unityObject && unityObject == null)
            {
                return null;
            }

            return hittable;
        }

        private static Vector3 ClosestPoint(Collider collider, Vector3 point)
        {
            // Collider.ClosestPoint は凸でない MeshCollider などに対応しないので、その場合は外接箱で近似する。
            var supported = collider is BoxCollider
                || collider is SphereCollider
                || collider is CapsuleCollider
                || (collider is MeshCollider mesh && mesh.convex);
            return supported ? collider.ClosestPoint(point) : collider.bounds.ClosestPoint(point);
        }
    }
}
