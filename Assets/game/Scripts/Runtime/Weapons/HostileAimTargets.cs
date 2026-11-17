using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    // エイムアシストは弾と違い撃った後に相手を追わないので、HitboxLockOnTargets の Id と Follow は持たず、狙う点だけを集める。
    // 見えるかは弾の通り道 (ShotCast) に加えて煙 (EnemySenses.IsVisionBlocked) でも確かめる。煙の向こうへ吸い付くと、見えない相手の位置が分かってしまうため。
    public sealed class HostileAimTargets : IAimAssistTargets
    {
        public const int MaxColliders = HitboxLockOnTargets.MaxColliders;

        private readonly Collider[] _overlaps = new Collider[MaxColliders];
        private readonly HashSet<Damageable> _seen = new HashSet<Damageable>();
        private readonly Transform _shooterRoot;
        private readonly int _layerMask;

        public HostileAimTargets(Transform shooterRoot, int layerMask)
        {
            _shooterRoot = shooterRoot;
            _layerMask = layerMask;
        }

        public void Collect(Vector3 eye, float range, List<NVector3> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            _seen.Clear();
            // 陣営は撃つ側が途中で変わることがある (チームの振り直し) ので、毎回引き直す。
            var self = _shooterRoot != null ? SideMember.Of(_shooterRoot.gameObject) : null;
            var count = Physics.OverlapSphereNonAlloc(eye, range, _overlaps, _layerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var owner = OwnerOf(_overlaps[i]);
                _overlaps[i] = null;
                if (owner == null || !_seen.Add(owner) || (_shooterRoot != null && owner.transform.IsChildOf(_shooterRoot)))
                {
                    continue;
                }

                if (self != null && self.IsAllyOf(SideMember.Of(owner.gameObject)))
                {
                    continue;
                }

                var point = owner.AimPoint;
                if (Vector3.Distance(eye, point) <= range && IsVisible(eye, point, owner))
                {
                    into.Add(point.ToNumerics());
                }
            }

            _seen.Clear();
        }

        void IAimAssistTargets.Collect(NVector3 eye, float range, List<NVector3> into)
        {
            Collect(eye.ToUnity(), range, into);
        }

        private static Damageable OwnerOf(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            var hitbox = Hitbox.FindFor(collider);
            var owner = hitbox != null ? hitbox.Owner : null;
            return owner != null && !owner.IsDead && !owner.IsDowned ? owner : null;
        }

        // 相手の体の別の部位が手前にあっても見えているので、最初に当たった物の持ち主で確かめる (HitboxLockOnTargets と同じ)。
        private bool IsVisible(Vector3 eye, Vector3 point, Damageable owner)
        {
            var offset = point - eye;
            var distance = offset.magnitude;
            if (distance < 1e-4f)
            {
                return true;
            }

            if (ShotCast.TryFirst(eye, offset / distance, distance, _layerMask, _shooterRoot, out var hit))
            {
                var hitbox = Hitbox.FindFor(hit.collider);
                if (hitbox == null || hitbox.Owner != owner)
                {
                    return false;
                }
            }

            return !EnemySenses.IsVisionBlocked(eye, point);
        }
    }
}
