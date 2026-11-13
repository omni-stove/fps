using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 陣営ごとの出現点から、倒れた人を戻す点を選ぶ。自陣営の点のうち、いちばん近い敵までの距離が最も長い点を選ぶ。
    /// 敵がいなければ (またはどの点も同じ距離なら) 一覧で先の点を選ぶ。乱数は使わない。
    /// </summary>
    /// <remarks>
    /// <see cref="SpawnSelector"/> は Alpha と Bravo の 2 チームに限るので、3 つ以上の陣営を持つモードのためにこちらを使う。
    /// </remarks>
    public static class SideSpawnSelector
    {
        /// <summary>side が使える点の添字を返す。side の点が 1 つも無ければ -1。</summary>
        /// <param name="spawns">出現点の一覧。</param>
        /// <param name="side">戻る人の陣営。</param>
        /// <param name="enemyPositions">生きている敵の位置。空でもよい。</param>
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
