using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    public static class StructureDamage
    {
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

            return SpreadAround(point, damage, radius);
        }

        public static int ApplyAround(Vector3 point, int damage, float radius)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            return SpreadAround(point, damage, radius);
        }

        private static int SpreadAround(Vector3 point, int damage, float radius)
        {
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
