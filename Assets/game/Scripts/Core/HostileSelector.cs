using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 陣営を持つ bot が狙う相手を選ぶ。自分と違う陣営で生きている候補のうち、いちばん近いものを選ぶ。
    /// 乱数は使わず、同じ距離なら一覧で先の候補を選ぶ。
    /// </summary>
    public static class HostileSelector
    {
        /// <summary>
        /// self の陣営から見た、selfPosition にいちばん近い敵の添字を返す。敵がいなければ -1。
        /// 位置が有限でない候補は選ばない (壊れた相手を追い続けないため)。
        /// </summary>
        /// <param name="self">選ぶ側の陣営。</param>
        /// <param name="selfPosition">選ぶ側の位置。各成分は有限の値。</param>
        /// <param name="candidates">候補。選ぶ側自身を含んでいてもよい (同じ陣営なので選ばれない)。</param>
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
