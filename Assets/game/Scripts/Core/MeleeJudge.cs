using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    // 座標は Y が上。前方の扇は水平面で測り、届く距離は高さの差も含めて測る (段の上の相手に下から届かないように)。
    public static class MeleeJudge
    {
        private const float _minLengthSquared = 1e-8f;

        public static bool IsInFan(Vector3 origin, Vector3 forward, MeleeSpec spec, Vector3 point, out float distance)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            distance = 0f;
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(point))
            {
                return false;
            }

            if (!TryFlatten(forward, out _))
            {
                return false;
            }

            distance = (point - origin).Length();
            return distance <= spec.Reach && IsWithinArc(origin, forward, spec.ArcDegrees, point);
        }

        // 距離は問わず、水平の向きだけを見る。前方が水平に決まらなければ false。
        public static bool IsWithinArc(Vector3 origin, Vector3 forward, float arcDegrees, Vector3 point)
        {
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(point) || !(arcDegrees > 0f))
            {
                return false;
            }

            if (!TryFlatten(forward, out var flatForward))
            {
                return false;
            }

            // 真上・真下や体に重なった相手は向きが決まらない。触れているほど近いので扇の中とみなす。
            if (!TryFlatten(point - origin, out var flatOffset))
            {
                return true;
            }

            var halfArc = Math.Min(arcDegrees, MeleeSpec.MaxArcDegrees) / 2.0;
            return Vector3.Dot(flatForward, flatOffset) >= Math.Cos(halfArc * Math.PI / 180.0) - 1e-6;
        }

        // 同じ距離なら先に並んでいるほうを選ぶ。呼ぶ側が並びを決めておけば、毎回同じ相手を選ぶ。
        public static int SelectNearest(Vector3 origin, Vector3 forward, MeleeSpec spec, IReadOnlyList<Vector3> candidates)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (IsInFan(origin, forward, spec, candidates[i], out var distance) && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // 怯みや吹き飛ばしが使う向き。上下に飛ばさないよう水平にし、決まらなければ振った向きにする。
        public static Vector3 HitDirection(Vector3 origin, Vector3 forward, Vector3 point)
        {
            if (IsFinite(origin) && IsFinite(point) && TryFlatten(point - origin, out var toTarget))
            {
                return toTarget;
            }

            return IsFinite(forward) && TryFlatten(forward, out var flatForward) ? flatForward : Vector3.Zero;
        }

        private static bool TryFlatten(Vector3 value, out Vector3 flat)
        {
            var horizontal = new Vector3(value.X, 0f, value.Z);
            var lengthSquared = horizontal.LengthSquared();
            if (!(lengthSquared >= _minLengthSquared) || float.IsInfinity(lengthSquared))
            {
                flat = Vector3.Zero;
                return false;
            }

            flat = horizontal / (float)Math.Sqrt(lengthSquared);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
