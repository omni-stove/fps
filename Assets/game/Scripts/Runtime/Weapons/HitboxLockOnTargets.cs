using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Fps.Runtime
{
    // オフラインの相手は Hitbox を持つ生きた Damageable。撃った本人の体は ShotCast と同じく外す。
    public sealed class HitboxLockOnTargets : ILockOnTargets
    {
        public const int MaxColliders = 256;

        private readonly Collider[] _overlaps = new Collider[MaxColliders];
        private readonly HashSet<Damageable> _seen = new HashSet<Damageable>();
        private readonly Transform _shooterRoot;
        private readonly int _layerMask;

        // Unity 6.6 では Object の Id を int にできないので (ShotWorldCollision と同じ)、初めて見た相手に連番を振る。
        private readonly Dictionary<Damageable, int> _ids = new Dictionary<Damageable, int>();
        private readonly List<Damageable> _byId = new List<Damageable>();

        public HitboxLockOnTargets(Transform shooterRoot, int layerMask)
        {
            _shooterRoot = shooterRoot;
            _layerMask = layerMask;
        }

        public void Collect(NVector3 eye, NVector3 forward, float range, List<LockOnCandidate> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            _seen.Clear();
            var origin = eye.ToUnity();
            var count = Physics.OverlapSphereNonAlloc(origin, range, _overlaps, _layerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var owner = OwnerOf(_overlaps[i]);
                if (owner == null || !_seen.Add(owner) || (_shooterRoot != null && owner.transform.IsChildOf(_shooterRoot)))
                {
                    continue;
                }

                var point = owner.AimPoint;
                if (Vector3.Distance(origin, point) <= range && IsVisible(origin, point, owner))
                {
                    into.Add(new LockOnCandidate(IdOf(owner), point.ToNumerics()));
                }
            }
        }

        public Func<NVector3?> Follow(int id)
        {
            if (id < 0 || id >= _byId.Count)
            {
                return null;
            }

            var owner = _byId[id];
            return () => owner != null && !owner.IsDead ? owner.AimPoint.ToNumerics() : (NVector3?)null;
        }

        private static Damageable OwnerOf(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            var hitbox = Hitbox.FindFor(collider);
            var owner = hitbox != null ? hitbox.Owner : null;
            return owner != null && !owner.IsDead ? owner : null;
        }

        private int IdOf(Damageable owner)
        {
            if (!_ids.TryGetValue(owner, out var id))
            {
                id = _byId.Count;
                _ids.Add(owner, id);
                _byId.Add(owner);
            }

            return id;
        }

        // 相手の体の別の部位が手前にあっても見えているので、最初に当たった物の持ち主で確かめる。
        private bool IsVisible(Vector3 origin, Vector3 point, Damageable owner)
        {
            var offset = point - origin;
            var distance = offset.magnitude;
            return distance < 1e-4f
                || !ShotCast.TryFirst(origin, offset / distance, distance, _layerMask, _shooterRoot, out var hit)
                || OwnerOf(hit.collider) == owner;
        }
    }
}
