using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static class HostileSelector
    {
        public static int Nearest(Side self, Vector3 selfPosition, IReadOnlyList<HostileCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!IsFinite(selfPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(selfPosition), selfPosition, "selfPosition must be finite");
            }

            var best = -1;
            var bestDistanceSquared = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                // 位置が有限でない候補を飛ばすのは、壊れた相手を追い続けないため。
                if (candidate.Side == self || !candidate.IsAlive || !IsFinite(candidate.Position))
                {
                    continue;
                }

                var distanceSquared = Vector3.DistanceSquared(selfPosition, candidate.Position);
                if (distanceSquared < bestDistanceSquared)
                {
                    best = i;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return best;
        }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
