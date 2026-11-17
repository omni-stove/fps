using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // SpawnSelector は Alpha と Bravo の 2 チームに限るので、3 つ以上の陣営を持つモードのためにこちらを使う。
    public static class SideSpawnSelector
    {
        public static int Select(IReadOnlyList<SideSpawn> spawns, Side side, IReadOnlyList<Vector3> enemyPositions)
        {
            if (spawns == null)
            {
                throw new ArgumentNullException(nameof(spawns));
            }

            if (enemyPositions == null)
            {
                throw new ArgumentNullException(nameof(enemyPositions));
            }

            var best = -1;
            var bestDistanceSquared = float.NegativeInfinity;
            for (var i = 0; i < spawns.Count; i++)
            {
                if (spawns[i].Side != side)
                {
                    continue;
                }

                var nearest = NearestDistanceSquared(spawns[i].Position, enemyPositions);
                if (nearest > bestDistanceSquared)
                {
                    best = i;
                    bestDistanceSquared = nearest;
                }
            }

            return best;
        }

        private static float NearestDistanceSquared(Vector3 from, IReadOnlyList<Vector3> positions)
        {
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < positions.Count; i++)
            {
                nearest = Math.Min(nearest, Vector3.DistanceSquared(from, positions[i]));
            }

            return nearest;
        }
    }
}
