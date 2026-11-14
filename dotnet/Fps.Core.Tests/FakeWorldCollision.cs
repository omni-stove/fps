using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core.Tests
{
    /// <summary>
    /// 軸に沿った箱と無限の半空間だけでできた世界。掃引は、凸な形への距離が掃引の距離に対して凸なことを使い、
    /// 黄金分割探索と二分探索で解く (速さより、どの形でも同じ方法で正しく解けることを取る)。
    /// </summary>
    internal sealed class FakeWorldCollision : IWorldCollision
    {
        private const int _searchIterations = 50;

        private readonly List<IShape> _shapes = new List<IShape>();

        private interface IShape
        {
            int Layer { get; }

            Vector3D Closest(Vector3D point);

            bool MayTouch(Vector3D a, Vector3D b, Vector3D direction, double distance, double radius);

            bool Raycast(Vector3D origin, Vector3D direction, double maxDistance, out double distance, out Vector3D normal);
        }

        public int AddBox(Vector3 min, Vector3 max, int layer = 0)
        {
            _shapes.Add(new Box(new Vector3D(min), new Vector3D(max), layer));
            return _shapes.Count - 1;
        }

        /// <summary>法線 normal の向きの外側を空ける、point を通る面。</summary>
        public int AddHalfSpace(Vector3 point, Vector3 normal, int layer = 0)
        {
            var n = new Vector3D(normal).Normalized();
            _shapes.Add(new HalfSpace(n, n.Dot(new Vector3D(point)), layer));
            return _shapes.Count - 1;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            var dir = new Vector3D(direction);
            if (dir.Length() == 0d || maxDistance <= 0f)
            {
                return false;
            }

            dir = dir.Normalized();
            var o = new Vector3D(origin);
            var best = double.MaxValue;
            var found = false;
            for (var i = 0; i < _shapes.Count; i++)
            {
                var shape = _shapes[i];
                if (!InMask(shape, layerMask) || !shape.Raycast(o, dir, maxDistance, out var distance, out var normal))
                {
                    continue;
                }

                if (distance < best)
                {
                    best = distance;
                    found = true;
                    hit = new WorldHit((o + (dir * distance)).ToVector3(), normal.ToVector3(), (float)distance, i, shape.Layer);
                }
            }

            return found;
        }

        public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
        {
            if (radius <= 0f)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < _shapes.Count && count < results.Length; i++)
            {
                var shape = _shapes[i];
                if (InMask(shape, layerMask)
                    && SegmentDistance(shape, new Vector3D(point0), new Vector3D(point1), out _, out _) < radius)
                {
                    results[count++] = i;
                }
            }

            return count;
        }

        public bool CapsuleCast(
            Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            var dir = new Vector3D(direction);
            if (radius <= 0f || maxDistance <= 0f || dir.Length() == 0d)
            {
                return false;
            }

            dir = dir.Normalized();
            var a = new Vector3D(point0);
            var b = new Vector3D(point1);
            var best = double.MaxValue;
            var found = false;
            for (var i = 0; i < _shapes.Count; i++)
            {
                var shape = _shapes[i];
                if (!InMask(shape, layerMask) || SegmentDistance(shape, a, b, out _, out _) < radius)
                {
                    continue;
                }

                double DistanceAt(double t) => SegmentDistance(shape, a + (dir * t), b + (dir * t), out _, out _);

                if (!shape.MayTouch(a, b, dir, maxDistance, radius))
                {
                    continue;
                }

                var nearest = MinimizeConvex(DistanceAt, 0d, maxDistance);
                if (DistanceAt(nearest) > radius)
                {
                    continue;
                }

                var before = 0d;
                var after = nearest;
                for (var k = 0; k < _searchIterations; k++)
                {
                    var mid = (before + after) * 0.5d;
                    if (DistanceAt(mid) <= radius)
                    {
                        after = mid;
                    }
                    else
                    {
                        before = mid;
                    }
                }

                if (after >= best)
                {
                    continue;
                }

                SegmentDistance(shape, a + (dir * after), b + (dir * after), out var onSegment, out var onShape);
                best = after;
                found = true;
                hit = new WorldHit(onShape.ToVector3(), (onSegment - onShape).Normalized().ToVector3(), (float)after, i, shape.Layer);
            }

            return found;
        }

        private static double MinimizeConvex(Func<double, double> f, double low, double high)
        {
            const double inverseGolden = 0.6180339887498949;
            var x1 = high - (inverseGolden * (high - low));
            var x2 = low + (inverseGolden * (high - low));
            var f1 = f(x1);
            var f2 = f(x2);
            for (var k = 0; k < _searchIterations; k++)
            {
                if (f1 <= f2)
                {
                    high = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = high - (inverseGolden * (high - low));
                    f1 = f(x1);
                }
                else
                {
                    low = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = low + (inverseGolden * (high - low));
                    f2 = f(x2);
                }
            }

            return (low + high) * 0.5d;
        }

        private static bool InMask(IShape shape, int layerMask)
        {
            return (layerMask & (1 << shape.Layer)) != 0;
        }

        private static double SegmentDistance(IShape shape, Vector3D a, Vector3D b, out Vector3D onSegment, out Vector3D onShape)
        {
            double DistanceAt(double s)
            {
                var p = a + ((b - a) * s);
                return (p - shape.Closest(p)).Length();
            }

            var s = MinimizeConvex(DistanceAt, 0d, 1d);
            onSegment = a + ((b - a) * s);
            onShape = shape.Closest(onSegment);
            return (onSegment - onShape).Length();
        }

        private sealed class Box : IShape
        {
            private readonly Vector3D _min;
            private readonly Vector3D _max;

            public Box(Vector3D min, Vector3D max, int layer)
            {
                _min = min;
                _max = max;
                Layer = layer;
            }

            public int Layer { get; }

            public bool MayTouch(Vector3D a, Vector3D b, Vector3D direction, double distance, double radius)
            {
                var end = direction * distance;
                for (var axis = 0; axis < 3; axis++)
                {
                    var low = Math.Min(Math.Min(a[axis], b[axis]), Math.Min(a[axis], b[axis]) + end[axis]) - radius;
                    var high = Math.Max(Math.Max(a[axis], b[axis]), Math.Max(a[axis], b[axis]) + end[axis]) + radius;
                    if (high < _min[axis] || low > _max[axis])
                    {
                        return false;
                    }
                }

                return true;
            }

            public Vector3D Closest(Vector3D point)
            {
                return new Vector3D(
                    Math.Clamp(point.X, _min.X, _max.X), Math.Clamp(point.Y, _min.Y, _max.Y), Math.Clamp(point.Z, _min.Z, _max.Z));
            }

            public bool Raycast(Vector3D origin, Vector3D direction, double maxDistance, out double distance, out Vector3D normal)
            {
                distance = 0d;
                normal = default;
                var enter = double.MinValue;
                var exit = double.MaxValue;
                var enterAxis = -1;
                var enterSign = 0d;
                for (var axis = 0; axis < 3; axis++)
                {
                    var o = origin[axis];
                    var d = direction[axis];
                    if (Math.Abs(d) < 1e-12)
                    {
                        if (o < _min[axis] || o > _max[axis])
                        {
                            return false;
                        }

                        continue;
                    }

                    var t1 = (_min[axis] - o) / d;
                    var t2 = (_max[axis] - o) / d;
                    var sign = -Math.Sign(d);
                    if (t1 > t2)
                    {
                        (t1, t2) = (t2, t1);
                    }

                    if (t1 > enter)
                    {
                        enter = t1;
                        enterAxis = axis;
                        enterSign = sign;
                    }

                    exit = Math.Min(exit, t2);
                }

                if (enterAxis < 0 || enter < 0d || enter > exit || enter > maxDistance)
                {
                    return false;
                }

                distance = enter;
                normal = Vector3D.Axis(enterAxis, enterSign);
                return true;
            }
        }

        private sealed class HalfSpace : IShape
        {
            private readonly Vector3D _normal;
            private readonly double _offset;

            public HalfSpace(Vector3D normal, double offset, int layer)
            {
                _normal = normal;
                _offset = offset;
                Layer = layer;
            }

            public int Layer { get; }

            public bool MayTouch(Vector3D a, Vector3D b, Vector3D direction, double distance, double radius)
            {
                return true;
            }

            public Vector3D Closest(Vector3D point)
            {
                var above = _normal.Dot(point) - _offset;
                return above > 0d ? point - (_normal * above) : point;
            }

            public bool Raycast(Vector3D origin, Vector3D direction, double maxDistance, out double distance, out Vector3D normal)
            {
                distance = 0d;
                normal = _normal;
                var above = _normal.Dot(origin) - _offset;
                var approach = _normal.Dot(direction);
                if (above < 0d || approach >= 0d)
                {
                    return false;
                }

                distance = above / -approach;
                return distance <= maxDistance;
            }
        }

        private readonly struct Vector3D
        {
            public Vector3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public Vector3D(Vector3 v)
                : this(v.X, v.Y, v.Z)
            {
            }

            public double X { get; }

            public double Y { get; }

            public double Z { get; }

            public double this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;

            public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

            public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

            public static Vector3D operator *(Vector3D a, double s) => new Vector3D(a.X * s, a.Y * s, a.Z * s);

            public static Vector3D Axis(int axis, double sign)
            {
                return new Vector3D(axis == 0 ? sign : 0d, axis == 1 ? sign : 0d, axis == 2 ? sign : 0d);
            }

            public double Dot(Vector3D other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

            public double Length() => Math.Sqrt(Dot(this));

            public Vector3D Normalized() => this * (1d / Length());

            public Vector3 ToVector3() => new Vector3((float)X, (float)Y, (float)Z);
        }
    }
}
