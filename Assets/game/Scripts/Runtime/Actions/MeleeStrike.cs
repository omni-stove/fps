using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // プレイヤー (MeleeController) と敵 (EnemyMelee) が同じ決まりで当てるよう、相手の探し方とダメージの渡し方をここにまとめる。
    public static class MeleeStrike
    {
        public readonly struct Result
        {
            public Result(Damageable target, Vector3 point, float distance, Vector3 direction)
            {
                Target = target;
                Point = point;
                Distance = distance;
                Direction = direction;
            }

            public Damageable Target { get; }

            public Vector3 Point { get; }

            public float Distance { get; }

            public Vector3 Direction { get; }
        }

        /// <exception cref="ArgumentNullException">attacker か spec が null。</exception>
        public static bool TryFind(Transform attacker, Vector3 origin, Vector3 forward, MeleeSpec spec, LayerMask mask, out Result result)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            result = default;
            var owners = new List<Damageable>();
            var points = new List<Vector3>();
            foreach (var collider in Physics.OverlapSphere(origin, spec.Reach, mask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(attacker))
                {
                    continue;
                }

                var owner = FindDamageable(collider);
                if (owner == null || owner.IsDead || owner.transform.IsChildOf(attacker))
                {
                    continue;
                }

                var point = ClosestPoint(collider, origin);
                var index = owners.IndexOf(owner);
                if (index < 0)
                {
                    owners.Add(owner);
                    points.Add(point);
                }
                else if ((point - origin).sqrMagnitude < (points[index] - origin).sqrMagnitude)
                {
                    points[index] = point;
                }
            }

            // OverlapSphere の並びは決まっていないので、同じ距離の相手を毎回同じに選ぶよう Id で並べ、壁越しの相手は外す。
            var candidates = new List<(Damageable Owner, Vector3 Point)>();
            for (var i = 0; i < owners.Count; i++)
            {
                if (!IsBlocked(attacker, origin, points[i], owners[i], mask))
                {
                    candidates.Add((owners[i], points[i]));
                }
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.Owner.Id, b.Owner.Id));
            var numerics = new System.Numerics.Vector3[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                numerics[i] = candidates[i].Point.ToNumerics();
            }

            var best = MeleeJudge.SelectNearest(origin.ToNumerics(), forward.ToNumerics(), spec, numerics);
            if (best < 0)
            {
                return false;
            }

            var chosen = candidates[best];
            var direction = MeleeJudge.HitDirection(origin.ToNumerics(), forward.ToNumerics(), chosen.Point.ToNumerics()).ToUnity();
            result = new Result(chosen.Owner, chosen.Point, Vector3.Distance(origin, chosen.Point), direction);
            return true;
        }

        // 武器の Id を帰属に入れるのは、キルフィードが近接で倒したときも持っていた武器を出せるようにするため。素手なら空。
        public static int Apply(GameObject attacker, Result hit, int damage, string weaponId)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (hit.Target == null)
            {
                throw new ArgumentException("hit has no target", nameof(hit));
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must be positive");
            }

            using (CombatEvents.Attribute(attacker, weaponId ?? string.Empty))
            {
                hit.Target.TakeDamage(damage, attacker.transform.position, hit.Direction, DamageKind.Melee);
            }

            return damage;
        }

        private static Damageable FindDamageable(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            var damageable = collider.GetComponentInParent<Damageable>();
            return damageable != null ? damageable : null;
        }

        private static bool IsBlocked(Transform attacker, Vector3 from, Vector3 to, Damageable target, LayerMask mask)
        {
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-4f)
            {
                return false;
            }

            foreach (var hit in Physics.RaycastAll(from, offset / length, length, mask, QueryTriggerInteraction.Ignore))
            {
                var hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(attacker) || hitTransform.IsChildOf(target.transform) || FindDamageable(hit.collider) == target)
                {
                    continue;
                }

                return true;
            }

            return false;
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
