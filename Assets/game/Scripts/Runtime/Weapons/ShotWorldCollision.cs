using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Fps.Runtime
{
    // Core の弾道 (BallisticFlight) を Unity の物理に当てる。撃った本人と、撃った者の弾を通す置いた物は ShotCast と同じく当たりから外す。
    public sealed class ShotWorldCollision : IWorldCollision
    {
        private static readonly Collider[] _overlaps = new Collider[ShotCast.MaxHits];
        private static readonly RaycastHit[] _casts = new RaycastHit[ShotCast.MaxHits];

        private readonly Transform _shooterRoot;

        // Unity 6.6 では Object の Id を int にできないので、この世界で初めて見たコライダーに連番を振る。
        private readonly List<Collider> _colliders = new List<Collider>();
        private readonly Dictionary<Collider, int> _ids = new Dictionary<Collider, int>();

        public ShotWorldCollision(Transform shooterRoot)
        {
            _shooterRoot = shooterRoot;
        }

        // WorldHit には Collider を載せられないので、着弾の効果を出すために最後の Raycast の当たりを Unity の形のまま残す。
        public RaycastHit LastHit { get; private set; }

        public bool Raycast(NVector3 origin, NVector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            var dir = direction.ToUnity();
            if (dir.sqrMagnitude < 1e-12f || !(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                return false;
            }

            if (!ShotCast.TryFirst(origin.ToUnity(), dir, maxDistance, layerMask, _shooterRoot, out var found))
            {
                return false;
            }

            LastHit = found;
            hit = ToWorldHit(found);
            return true;
        }

        public bool RaycastCollider(int colliderId, NVector3 origin, NVector3 direction, float maxDistance, out WorldHit hit)
        {
            hit = default;
            var dir = direction.ToUnity();
            var collider = colliderId >= 0 && colliderId < _colliders.Count ? _colliders[colliderId] : null;
            if (collider == null || !collider.enabled || dir.sqrMagnitude < 1e-12f || !(maxDistance > 0f))
            {
                return false;
            }

            if (!collider.Raycast(new Ray(origin.ToUnity(), dir.normalized), out var found, maxDistance))
            {
                return false;
            }

            hit = ToWorldHit(found);
            return true;
        }

        public int OverlapCapsule(NVector3 point0, NVector3 point1, float radius, int layerMask, int[] results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!(radius > 0f))
            {
                return 0;
            }

            var count = Physics.OverlapCapsuleNonAlloc(
                point0.ToUnity(), point1.ToUnity(), radius, _overlaps, layerMask, QueryTriggerInteraction.Ignore);
            var written = 0;
            for (var i = 0; i < count && written < results.Length; i++)
            {
                if (!IsShooters(_overlaps[i]))
                {
                    results[written++] = IdOf(_overlaps[i]);
                }
            }

            Array.Clear(_overlaps, 0, count);
            return written;
        }

        public bool CapsuleCast(
            NVector3 point0, NVector3 point1, float radius, NVector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            var dir = direction.ToUnity();
            if (!(radius > 0f) || !(maxDistance > 0f) || dir.sqrMagnitude < 1e-12f)
            {
                return false;
            }

            var count = Physics.CapsuleCastNonAlloc(
                point0.ToUnity(), point1.ToUnity(), radius, dir.normalized, _casts, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
            var found = false;
            var best = default(RaycastHit);
            for (var i = 0; i < count; i++)
            {
                var candidate = _casts[i];

                // Unity は動かす前から重なっている物を距離 0 で返すが、IWorldCollision はそれを当たりにしない。
                if (candidate.distance <= 0f || IsShooters(candidate.collider))
                {
                    continue;
                }

                if (!found || candidate.distance < best.distance)
                {
                    best = candidate;
                    found = true;
                }
            }

            Array.Clear(_casts, 0, count);
            if (found)
            {
                hit = ToWorldHit(best);
            }

            return found;
        }

        private bool IsShooters(Collider collider)
        {
            if (_shooterRoot == null)
            {
                return false;
            }

            return collider.transform.IsChildOf(_shooterRoot) || Deployable.LetsThrough(collider, _shooterRoot.gameObject);
        }

        private WorldHit ToWorldHit(RaycastHit hit)
        {
            return new WorldHit(hit.point.ToNumerics(), hit.normal.ToNumerics(), hit.distance, IdOf(hit.collider), hit.collider.gameObject.layer);
        }

        private int IdOf(Collider collider)
        {
            if (!_ids.TryGetValue(collider, out var id))
            {
                id = _colliders.Count;
                _colliders.Add(collider);
                _ids.Add(collider, id);
            }

            return id;
        }
    }
}
