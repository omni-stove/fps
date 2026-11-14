using System;
using System.Numerics;

namespace Fps.Core
{
    internal static class WorldGeometry
    {
        private const float _parallelEpsilon = 1e-6f;

        public static float Axis(Vector3 v, int axis)
        {
            return axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        }

        public static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var lengthSq = Vector3.Dot(ab, ab);
            if (lengthSq <= 0f)
            {
                return a;
            }

            var s = Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return a + (ab * s);
        }

        public static float ClosestPointsSegmentSegment(
            Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 onFirst, out Vector3 onSecond)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Vector3.Dot(d1, d1);
            var e = Vector3.Dot(d2, d2);
            var f = Vector3.Dot(d2, r);
            float s;
            float t;
            if (a <= 0f && e <= 0f)
            {
                s = 0f;
                t = 0f;
            }
            else if (a <= 0f)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                var c = Vector3.Dot(d1, r);
                if (e <= 0f)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    var b = Vector3.Dot(d1, d2);
                    var denom = (a * e) - (b * b);
                    s = denom > _parallelEpsilon * a * e ? Clamp01(((b * f) - (c * e)) / denom) : 0f;
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
            }

            onFirst = p1 + (d1 * s);
            onSecond = p2 + (d2 * t);
            return Vector3.DistanceSquared(onFirst, onSecond);
        }

        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
            {
                return a;
            }

            var bp = p - b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                return b;
            }

            var vc = (d1 * d4) - (d3 * d2);
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                return a + (ab * (d1 / (d1 - d3)));
            }

            var cp = p - c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                return c;
            }

            var vb = (d5 * d2) - (d1 * d6);
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                return a + (ac * (d2 / (d2 - d6)));
            }

            var va = (d3 * d6) - (d5 * d4);
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                return b + ((c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));
            }

            var denom = 1f / (va + vb + vc);
            return a + (ab * (vb * denom)) + (ac * (vc * denom));
        }

        public static bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var v0 = b - a;
            var v1 = c - a;
            var v2 = p - a;
            var d00 = Vector3.Dot(v0, v0);
            var d01 = Vector3.Dot(v0, v1);
            var d11 = Vector3.Dot(v1, v1);
            var d20 = Vector3.Dot(v2, v0);
            var d21 = Vector3.Dot(v2, v1);
            var denom = (d00 * d11) - (d01 * d01);
            if (!(denom > 0f))
            {
                return false;
            }

            var v = ((d11 * d20) - (d01 * d21)) / denom;
            var w = ((d00 * d21) - (d01 * d20)) / denom;
            return v >= 0f && w >= 0f && v + w <= 1f;
        }

        public static float SegmentTriangleDistanceSq(
            Vector3 p, Vector3 q, Vector3 a, Vector3 b, Vector3 c, Vector3 normal, out Vector3 onSegment, out Vector3 onTriangle)
        {
            var dp = Vector3.Dot(normal, p - a);
            var dq = Vector3.Dot(normal, q - a);
            if ((dp <= 0f && dq >= 0f) || (dp >= 0f && dq <= 0f))
            {
                var denom = dp - dq;
                var crossing = denom != 0f ? p + ((q - p) * (dp / denom)) : p;
                if (PointInTriangle(crossing, a, b, c))
                {
                    onSegment = crossing;
                    onTriangle = crossing;
                    return 0f;
                }
            }

            onSegment = p;
            onTriangle = ClosestPointOnTriangle(p, a, b, c);
            var best = Vector3.DistanceSquared(p, onTriangle);
            KeepCloser(q, ClosestPointOnTriangle(q, a, b, c), ref best, ref onSegment, ref onTriangle);
            KeepCloserSegment(p, q, a, b, ref best, ref onSegment, ref onTriangle);
            KeepCloserSegment(p, q, b, c, ref best, ref onSegment, ref onTriangle);
            KeepCloserSegment(p, q, c, a, ref best, ref onSegment, ref onTriangle);
            return best;
        }

        public static float SegmentBoxDistanceSq(
            Vector3 p, Vector3 q, Vector3 half, out Vector3 onSegment, out Vector3 onBox)
        {
            if (SegmentEntersBox(p, q, half, out var entry))
            {
                onSegment = entry;
                onBox = entry;
                return 0f;
            }

            onSegment = p;
            onBox = Vector3.Clamp(p, -half, half);
            var best = Vector3.DistanceSquared(p, onBox);
            KeepCloser(q, Vector3.Clamp(q, -half, half), ref best, ref onSegment, ref onBox);
            for (var axis = 0; axis < 3; axis++)
            {
                for (var corner = 0; corner < 4; corner++)
                {
                    BoxEdge(half, axis, corner, out var e0, out var e1);
                    KeepCloserSegment(p, q, e0, e1, ref best, ref onSegment, ref onBox);
                }
            }

            return best;
        }

        public static void BoxEdge(Vector3 half, int axis, int corner, out Vector3 start, out Vector3 end)
        {
            var u = (corner & 1) == 0 ? -1f : 1f;
            var v = (corner & 2) == 0 ? -1f : 1f;
            switch (axis)
            {
                case 0:
                    start = new Vector3(-half.X, u * half.Y, v * half.Z);
                    end = new Vector3(half.X, u * half.Y, v * half.Z);
                    break;
                case 1:
                    start = new Vector3(u * half.X, -half.Y, v * half.Z);
                    end = new Vector3(u * half.X, half.Y, v * half.Z);
                    break;
                default:
                    start = new Vector3(u * half.X, v * half.Y, -half.Z);
                    end = new Vector3(u * half.X, v * half.Y, half.Z);
                    break;
            }
        }

        public static bool RayAabb(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float maxDistance)
        {
            var tMin = 0f;
            var tMax = maxDistance;
            return Slab(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax)
                && Slab(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax)
                && Slab(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax);
        }

        public static bool AabbOverlap(Vector3 minA, Vector3 maxA, Vector3 minB, Vector3 maxB)
        {
            return minA.X <= maxB.X && maxA.X >= minB.X
                && minA.Y <= maxB.Y && maxA.Y >= minB.Y
                && minA.Z <= maxB.Z && maxA.Z >= minB.Z;
        }

        public static bool RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance)
        {
            // 遠くの始点のまま解くと float の桁落ちで当たりを外すので、球の手前まで始点を進めてから解く
            var skip = Math.Max(0f, Vector3.Dot(center - origin, direction) - radius);
            var m = origin + (direction * skip) - center;
            var b = Vector3.Dot(m, direction);
            var c = Vector3.Dot(m, m) - (radius * radius);
            if (c > 0f && b > 0f)
            {
                distance = 0f;
                return false;
            }

            var discriminant = (b * b) - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            distance = skip + Math.Max(0f, -b - (float)Math.Sqrt(discriminant));
            return true;
        }

        public static bool RayCapsule(
            Vector3 origin, Vector3 direction, Vector3 p, Vector3 q, float radius, out float distance, out Vector3 normal)
        {
            var axisPoint = ClosestPointOnSegment(origin, p, q);
            var outward = origin - axisPoint;
            if (Vector3.Dot(outward, outward) <= radius * radius)
            {
                distance = 0f;
                normal = SafeNormalize(outward, -direction);
                return true;
            }

            var ab = q - p;
            var abab = Vector3.Dot(ab, ab);
            var center = (p + q) * 0.5f;
            var skip = Math.Max(0f, Vector3.Dot(center - origin, direction) - ((float)Math.Sqrt(abab) * 0.5f) - radius);
            var start = origin + (direction * skip);
            var best = float.PositiveInfinity;

            if (abab > 0f)
            {
                var m = start - p;
                var abd = Vector3.Dot(ab, direction);
                var abm = Vector3.Dot(ab, m);
                var a = abab - (abd * abd);
                if (a > _parallelEpsilon * abab)
                {
                    var b = (abab * Vector3.Dot(m, direction)) - (abm * abd);
                    var c = (abab * Vector3.Dot(m, m)) - (abm * abm) - (radius * radius * abab);
                    var discriminant = (b * b) - (a * c);
                    if (discriminant >= 0f)
                    {
                        var t = (-b - (float)Math.Sqrt(discriminant)) / a;
                        var s = abm + (t * abd);
                        if (t >= 0f && s >= 0f && s <= abab)
                        {
                            best = t;
                        }
                    }
                }
            }

            if (RaySphere(start, direction, p, radius, out var tp) && tp < best)
            {
                best = tp;
            }

            if (RaySphere(start, direction, q, radius, out var tq) && tq < best)
            {
                best = tq;
            }

            if (float.IsPositiveInfinity(best))
            {
                distance = 0f;
                normal = Vector3.Zero;
                return false;
            }

            var hitPoint = start + (direction * best);
            normal = SafeNormalize(hitPoint - ClosestPointOnSegment(hitPoint, p, q), -direction);
            distance = skip + best;
            return true;
        }

        public static bool RayTriangle(
            Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            // Unity の既定 (queriesHitBackfaces = false) と同じく、表 (Cross(b - a, c - a) の向き) からだけ当てる
            var e1 = b - a;
            var e2 = c - a;
            var pvec = Vector3.Cross(direction, e2);
            var det = Vector3.Dot(e1, pvec);
            distance = 0f;
            if (!(det > 0f))
            {
                return false;
            }

            var tvec = origin - a;
            var u = Vector3.Dot(tvec, pvec);
            if (u < 0f || u > det)
            {
                return false;
            }

            var qvec = Vector3.Cross(tvec, e1);
            var v = Vector3.Dot(direction, qvec);
            if (v < 0f || u + v > det)
            {
                return false;
            }

            distance = Vector3.Dot(e2, qvec) / det;
            return distance >= 0f;
        }

        public static bool RayBox(Vector3 origin, Vector3 direction, Vector3 half, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = Vector3.Zero;
            if (Math.Abs(origin.X) <= half.X && Math.Abs(origin.Y) <= half.Y && Math.Abs(origin.Z) <= half.Z)
            {
                return false;
            }

            var tMin = float.NegativeInfinity;
            var tMax = float.PositiveInfinity;
            var enterAxis = -1;
            for (var axis = 0; axis < 3; axis++)
            {
                var o = Axis(origin, axis);
                var d = Axis(direction, axis);
                var h = Axis(half, axis);
                if (d == 0f)
                {
                    if (o < -h || o > h)
                    {
                        return false;
                    }

                    continue;
                }

                var t1 = (-h - o) / d;
                var t2 = (h - o) / d;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    enterAxis = axis;
                }

                if (t2 < tMax)
                {
                    tMax = t2;
                }

                if (tMin > tMax)
                {
                    return false;
                }
            }

            if (enterAxis < 0 || tMin < 0f)
            {
                return false;
            }

            distance = tMin;
            var sign = Axis(direction, enterAxis) > 0f ? -1f : 1f;
            normal = enterAxis == 0 ? new Vector3(sign, 0f, 0f) : enterAxis == 1 ? new Vector3(0f, sign, 0f) : new Vector3(0f, 0f, sign);
            return true;
        }

        public static void SweepSegmentSegment(
            Vector3 a0, Vector3 a1, Vector3 direction, Vector3 b0, Vector3 b1, float radius,
            ref float best, ref Vector3 bestNormal)
        {
            // 動く線分 A と止まった線分 B の距離が radius になる時刻は、平行四辺形 B - A を radius だけ太らせた形へ
            // 原点から direction に撃った光線の当たりと同じ。太らせた形は 4 辺のカプセルと、面の厚み (平行でないとき) に分かれる
            var c00 = b0 - a0;
            var c10 = b1 - a0;
            var c11 = b1 - a1;
            var c01 = b0 - a1;
            SweepEdge(direction, c00, c10, radius, ref best, ref bestNormal);
            SweepEdge(direction, c10, c11, radius, ref best, ref bestNormal);
            SweepEdge(direction, c11, c01, radius, ref best, ref bestNormal);
            SweepEdge(direction, c01, c00, radius, ref best, ref bestNormal);

            var e1 = b1 - b0;
            var e2 = a0 - a1;
            var cross = Vector3.Cross(e1, e2);
            var crossSq = Vector3.Dot(cross, cross);
            if (!(crossSq > _parallelEpsilon * Vector3.Dot(e1, e1) * Vector3.Dot(e2, e2)))
            {
                return;
            }

            var n = cross / (float)Math.Sqrt(crossSq);
            if (!SlabEntry(Vector3.Zero, direction, c00, n, radius, out var t, out var faceNormal) || !(t < best))
            {
                return;
            }

            var w = (direction * t) - (faceNormal * radius) - c00;
            var g11 = Vector3.Dot(e1, e1);
            var g12 = Vector3.Dot(e1, e2);
            var g22 = Vector3.Dot(e2, e2);
            var r1 = Vector3.Dot(w, e1);
            var r2 = Vector3.Dot(w, e2);
            var det = (g11 * g22) - (g12 * g12);
            var u = ((g22 * r1) - (g12 * r2)) / det;
            var v = ((g11 * r2) - (g12 * r1)) / det;
            if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
            {
                best = t;
                bestNormal = faceNormal;
            }
        }

        public static bool SlabEntry(
            Vector3 origin, Vector3 direction, Vector3 planePoint, Vector3 planeNormal, float radius,
            out float distance, out Vector3 faceNormal)
        {
            var s0 = Vector3.Dot(planeNormal, origin - planePoint);
            var dn = Vector3.Dot(planeNormal, direction);
            if (s0 >= radius && dn < 0f)
            {
                distance = (s0 - radius) / -dn;
                faceNormal = planeNormal;
                return true;
            }

            if (s0 <= -radius && dn > 0f)
            {
                distance = (-radius - s0) / dn;
                faceNormal = -planeNormal;
                return true;
            }

            distance = 0f;
            faceNormal = Vector3.Zero;
            return false;
        }

        public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            var lengthSq = Vector3.Dot(v, v);
            return lengthSq > 0f ? v / (float)Math.Sqrt(lengthSq) : fallback;
        }

        private static void SweepEdge(Vector3 direction, Vector3 c0, Vector3 c1, float radius, ref float best, ref Vector3 bestNormal)
        {
            if (RayCapsule(Vector3.Zero, direction, c0, c1, radius, out var t, out var normal) && t < best)
            {
                best = t;
                bestNormal = normal;
            }
        }

        private static bool SegmentEntersBox(Vector3 p, Vector3 q, Vector3 half, out Vector3 entry)
        {
            var d = q - p;
            var tMin = 0f;
            var tMax = 1f;
            var inside = Slab(p.X, d.X, -half.X, half.X, ref tMin, ref tMax)
                && Slab(p.Y, d.Y, -half.Y, half.Y, ref tMin, ref tMax)
                && Slab(p.Z, d.Z, -half.Z, half.Z, ref tMin, ref tMax);
            entry = p + (d * tMin);
            return inside;
        }

        private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (direction == 0f)
            {
                return origin >= min && origin <= max;
            }

            var inverse = 1f / direction;
            var t1 = (min - origin) * inverse;
            var t2 = (max - origin) * inverse;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            if (t1 > tMin)
            {
                tMin = t1;
            }

            if (t2 < tMax)
            {
                tMax = t2;
            }

            return tMin <= tMax;
        }

        private static void KeepCloser(Vector3 onSegment, Vector3 onShape, ref float best, ref Vector3 bestSegment, ref Vector3 bestShape)
        {
            var distanceSq = Vector3.DistanceSquared(onSegment, onShape);
            if (distanceSq < best)
            {
                best = distanceSq;
                bestSegment = onSegment;
                bestShape = onShape;
            }
        }

        private static void KeepCloserSegment(
            Vector3 p, Vector3 q, Vector3 e0, Vector3 e1, ref float best, ref Vector3 bestSegment, ref Vector3 bestShape)
        {
            var distanceSq = ClosestPointsSegmentSegment(p, q, e0, e1, out var onSegment, out var onEdge);
            if (distanceSq < best)
            {
                best = distanceSq;
                bestSegment = onSegment;
                bestShape = onEdge;
            }
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
