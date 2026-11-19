using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // 進みを割合でなく始点からの道のり (m) で持つのは、速さ (m/秒) をそのまま足せるようにするため。
    // 中継点を点の番号で持つのは、マップでは道筋の曲がり角に中継点を置くことが多く、道のりで書くと点を動かすたびに書き直しになるため。
    public sealed class PayloadRoute
    {
        public const int MinPointCount = 2;

        private readonly Vector3[] _points;
        private readonly double[] _distances;
        private readonly int[] _checkpoints;

        public PayloadRoute(IReadOnlyList<Vector3> points, IReadOnlyList<int> checkpointPoints = null)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count < MinPointCount)
            {
                throw new ArgumentException(
                    "a payload route needs at least " + MinPointCount.ToString(CultureInfo.InvariantCulture) + " points but has "
                    + points.Count.ToString(CultureInfo.InvariantCulture),
                    nameof(points));
            }

            _points = new Vector3[points.Count];
            _distances = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!IsFinite(point))
                {
                    throw new ArgumentException("point " + Number(i) + " must be finite", nameof(points));
                }

                _points[i] = point;
                if (i == 0)
                {
                    continue;
                }

                // 長さ 0 の区間は向きが決まらず、台車の向きが跳ねるので受け付けない。
                var length = Vector3.Distance(_points[i - 1], point);
                if (!(length > 0f))
                {
                    throw new ArgumentException("point " + Number(i) + " is at the same place as point " + Number(i - 1), nameof(points));
                }

                _distances[i] = _distances[i - 1] + length;
            }

            _checkpoints = CopyCheckpoints(checkpointPoints, _points.Length);
        }

        public int PointCount => _points.Length;

        public double Length => _distances[_distances.Length - 1];

        public int CheckpointCount => _checkpoints.Length;

        public Vector3 Point(int index)
        {
            RequirePoint(index);
            return _points[index];
        }

        public double DistanceAt(int pointIndex)
        {
            RequirePoint(pointIndex);
            return _distances[pointIndex];
        }

        public int CheckpointPoint(int checkpoint)
        {
            RequireCheckpoint(checkpoint);
            return _checkpoints[checkpoint];
        }

        public double CheckpointDistance(int checkpoint)
        {
            RequireCheckpoint(checkpoint);
            return _distances[_checkpoints[checkpoint]];
        }

        public double Clamp(double distance)
        {
            if (double.IsNaN(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be NaN");
            }

            return Math.Max(0d, Math.Min(Length, distance));
        }

        public float FractionAt(double distance)
        {
            return (float)(Clamp(distance) / Length);
        }

        public Vector3 PositionAt(double distance)
        {
            var clamped = Clamp(distance);
            var segment = SegmentAt(clamped);
            var start = _distances[segment];
            var t = (float)((clamped - start) / (_distances[segment + 1] - start));
            return Vector3.Lerp(_points[segment], _points[segment + 1], t);
        }

        public Vector3 DirectionAt(double distance)
        {
            var segment = SegmentAt(Clamp(distance));
            return Vector3.Normalize(_points[segment + 1] - _points[segment]);
        }

        public int CheckpointsReachedBy(double distance)
        {
            var clamped = Clamp(distance);
            var count = 0;
            while (count < _checkpoints.Length && _distances[_checkpoints[count]] <= clamped)
            {
                count++;
            }

            return count;
        }

        private int SegmentAt(double distance)
        {
            var last = _points.Length - 2;
            for (var i = 0; i < last; i++)
            {
                if (distance < _distances[i + 1])
                {
                    return i;
                }
            }

            return last;
        }

        private void RequirePoint(int index)
        {
            if (index < 0 || index >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "no route point with this index");
            }
        }

        private void RequireCheckpoint(int checkpoint)
        {
            if (checkpoint < 0 || checkpoint >= _checkpoints.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, "no checkpoint with this index");
            }
        }

        // 始点は出発の位置で、終点は届けば試合が終わるので、どちらも中継点にはしない。
        private static int[] CopyCheckpoints(IReadOnlyList<int> checkpointPoints, int pointCount)
        {
            if (checkpointPoints == null || checkpointPoints.Count == 0)
            {
                return Array.Empty<int>();
            }

            var copy = new int[checkpointPoints.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var point = checkpointPoints[i];
                if (point <= 0 || point >= pointCount - 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(checkpointPoints), point,
                        "checkpoint " + Number(i) + " must name a point between the first and the last (1 to " + Number(pointCount - 2) + ")");
                }

                if (i > 0 && point <= copy[i - 1])
                {
                    throw new ArgumentException(
                        "checkpoints must name points in route order without repeats, but checkpoint " + Number(i) + " names point " + Number(point)
                        + " after point " + Number(copy[i - 1]),
                        nameof(checkpointPoints));
                }

                copy[i] = point;
            }

            return copy;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
