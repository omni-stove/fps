using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 照準の先の何に印を付けるかを決める。照準の Ray が最初に当たったものと、印を付けられる敵の一覧から選ぶ。
    /// <list type="number">
    /// <item>Ray が生きている敵に当たっていれば、その敵。</item>
    /// <item>そうでなければ、見えていて生きている敵のうち、照準との角度が <see cref="PingSpec.EnemyAimToleranceDegrees"/> 以内で最も小さい敵
    /// (同じ角度なら近い方、それも同じなら一覧で先の方)。</item>
    /// <item>敵が無ければ、Ray が当たった点 (場所)。</item>
    /// <item>どれも <see cref="PingSpec.MaxDistance"/> より遠ければ何も無い。</item>
    /// </list>
    /// </summary>
    public static class PingAim
    {
        private const float _minDistance = 1e-4f;

        /// <param name="spec">届く距離と敵に吸い付く角度。</param>
        /// <param name="origin">照準の始点。各成分は有限の値。</param>
        /// <param name="direction">照準の向き。長さは問わないが 0 ではない有限の値。</param>
        /// <param name="surfacePoint">Ray が最初に当たった点。何にも当たらなければ null。</param>
        /// <param name="surfaceCandidate">Ray が最初に当たったものが candidates の敵なら、その添字。敵でなければ -1。</param>
        /// <param name="candidates">印を付けられる敵。</param>
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
