using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static class PingAim
    {
        private const float _minDistance = 1e-4f;

        public static PingHit Resolve(
            PingSpec spec,
            Vector3 origin,
            Vector3 direction,
            Vector3? surfacePoint,
            int surfaceCandidate,
            IReadOnlyList<PingCandidate> candidates)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "origin must be finite");
            }

            if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (surfacePoint.HasValue && !IsFinite(surfacePoint.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(surfacePoint), surfacePoint, "surfacePoint must be finite");
            }

            if (surfaceCandidate < -1 || surfaceCandidate >= candidates.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(surfaceCandidate), surfaceCandidate, "surfaceCandidate must be -1 or an index into candidates");
            }

            if (surfaceCandidate >= 0)
            {
                var hit = candidates[surfaceCandidate];
                if (hit.IsAlive && IsFinite(hit.Position) && Vector3.Distance(origin, hit.Position) <= spec.MaxDistance)
                {
                    return PingHit.Enemy(surfaceCandidate, hit.Position);
                }
            }

            var forward = Vector3.Normalize(direction);
            var cosLimit = Math.Cos(spec.EnemyAimToleranceDegrees * Math.PI / 180.0);
            var best = -1;
            var bestCos = double.NegativeInfinity;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsAlive || !candidate.IsVisible || !IsFinite(candidate.Position))
                {
                    continue;
                }

                var toCandidate = candidate.Position - origin;
                var distance = toCandidate.Length();
                if (distance < _minDistance || distance > spec.MaxDistance)
                {
                    continue;
                }

                var cos = Math.Min(1.0, Vector3.Dot(toCandidate / distance, forward));
                if (cos < cosLimit)
                {
                    continue;
                }

                if (cos > bestCos || (cos == bestCos && distance < bestDistance))
                {
                    best = i;
                    bestCos = cos;
                    bestDistance = distance;
                }
            }

            if (best >= 0)
            {
                return PingHit.Enemy(best, candidates[best].Position);
            }

            if (surfacePoint.HasValue && Vector3.Distance(origin, surfacePoint.Value) <= spec.MaxDistance)
            {
                return PingHit.Location(surfacePoint.Value);
            }

            return PingHit.None;
        }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
