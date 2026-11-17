using System;
using System.Numerics;

namespace Shooter.Core
{
    // 遮られているかを見ないのは、世界の当たりを知るのがオフラインの SprayDelivery とサーバーの ServerSpray だけだから。
    public static class SprayCone
    {
        private const float _sameSpot = 1e-5f;

        private const float _degenerate = 1e-12f;

        // 縁ちょうどの点を float の丸めで外に出さないための余裕
        private const float _cosineTolerance = 1e-6f;

        public static bool Contains(SpraySpec spray, float range, Vector3 origin, Vector3 aim, Vector3 target)
        {
            if (spray == null)
            {
                throw new ArgumentNullException(nameof(spray));
            }

            var unit = Validate(range, origin, aim);
            if (!IsFinite(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "target must be finite");
            }

            var toTarget = target - origin;
            var distance = toTarget.Length();
            if (distance > range)
            {
                return false;
            }

            if (distance < _sameSpot)
            {
                return true;
            }

            var cosine = Vector3.Dot(toTarget / distance, unit);
            return cosine >= MathF.Cos(spray.ConeDegrees * (MathF.PI / 180f)) - _cosineTolerance;
        }

        // 体の中心でなく軸にいちばん近い体の点で Contains を確かめるのは、体の端が円錐にかかっていれば当てるため。
        public static Vector3 NearestPoint(float range, Vector3 origin, Vector3 aim, Vector3 segmentStart, Vector3 segmentEnd, float radius)
        {
            var unit = Validate(range, origin, aim);
            if (!IsFinite(segmentStart) || !IsFinite(segmentEnd))
            {
                throw new ArgumentOutOfRangeException(nameof(segmentStart), "the segment must be finite");
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            ClosestPoints(origin, origin + (unit * range), segmentStart, segmentEnd, out var onAxis, out var onSegment);
            var offset = onAxis - onSegment;
            var gap = offset.Length();
            return gap <= radius ? onAxis : onSegment + (offset * (radius / gap));
        }

        private static void ClosestPoints(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out Vector3 onP, out Vector3 onQ)
        {
            var d1 = p1 - p0;
            var d2 = q1 - q0;
            var r = p0 - q0;
            var a = Vector3.Dot(d1, d1);
            var e = Vector3.Dot(d2, d2);
            var f = Vector3.Dot(d2, r);
            float s;
            float t;
            if (e <= _degenerate)
            {
                s = a <= _degenerate ? 0f : Clamp01(-Vector3.Dot(d1, r) / a);
                t = 0f;
            }
            else
            {
                var c = Vector3.Dot(d1, r);
                var b = Vector3.Dot(d1, d2);
                var denominator = (a * e) - (b * b);
                s = denominator > _degenerate ? Clamp01(((b * f) - (c * e)) / denominator) : 0f;
                t = ((b * s) + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Clamp01((b - c) / a);
                }
            }

            onP = p0 + (d1 * s);
            onQ = q0 + (d2 * t);
        }

        private static Vector3 Validate(float range, Vector3 origin, Vector3 aim)
        {
            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "origin must be finite");
            }

            if (!IsFinite(aim) || aim.LengthSquared() < _degenerate)
            {
                throw new ArgumentOutOfRangeException(nameof(aim), aim, "aim must be finite and not zero");
            }

            return Vector3.Normalize(aim);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }
}
