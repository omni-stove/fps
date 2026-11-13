using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 乗り越えの軌跡。始点からの変位 (m、Y が上) を結んだ折れ線を、一定の速さ <see cref="Speed"/> でたどる。
    /// 衝突判定はせず、<see cref="Displacement"/> の戻り値を呼ぶ側が CharacterController.Move などに渡す。
    /// </summary>
    public sealed class VaultPath
    {
        private readonly Vector3[] _points;

        // _cumulative[i] は始点から _points[i] までの折れ線の長さ。
        private readonly float[] _cumulative;

        /// <param name="points">折れ線の頂点 (始点からの変位)。2 つ以上で、各成分が有限値。</param>
        /// <param name="speed">たどる速さ (m/s)。正の有限値。</param>
        public VaultPath(IReadOnlyList<Vector3> points, float speed)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count < 2)
            {
                throw new ArgumentException("points must have at least two points", nameof(points));
            }

            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and positive");
            }

            _points = new Vector3[points.Count];
            _cumulative = new float[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
                {
                    throw new ArgumentOutOfRangeException(nameof(points), point, "points must be finite");
                }

                _points[i] = point;
                _cumulative[i] = i == 0 ? 0f : _cumulative[i - 1] + Vector3.Distance(_points[i - 1], point);
            }

            Speed = speed;
            Length = _cumulative[_cumulative.Length - 1];
            Duration = Length / speed;
        }

        /// <summary>折れ線の頂点 (始点からの変位)。</summary>
        public IReadOnlyList<Vector3> Points => _points;

        /// <summary>たどる速さ (m/s)。</summary>
        public float Speed { get; }

        /// <summary>折れ線の長さ (m)。</summary>
        public float Length { get; }

        /// <summary>たどり終えるまでの秒数。</summary>
        public float Duration { get; }

        /// <summary>終点 (始点からの変位)。</summary>
        public Vector3 End => _points[_points.Length - 1];

        /// <summary>始めてから seconds 秒の位置 (始点からの変位)。<see cref="Duration"/> を過ぎたら終点。</summary>
        /// <param name="seconds">0 以上。無限大は終点として扱う。</param>
        public Vector3 PositionAt(float seconds)
        {
            if (!(seconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            var travelled = seconds * Speed;
            if (travelled >= Length)
            {
                return End;
            }

            for (var i = 1; i < _points.Length; i++)
            {
                if (travelled > _cumulative[i])
                {
                    continue;
                }

                var segment = _cumulative[i] - _cumulative[i - 1];
                var t = segment > 0f ? (travelled - _cumulative[i - 1]) / segment : 1f;
                return Vector3.Lerp(_points[i - 1], _points[i], t);
            }

            return End;
        }

        /// <summary>
        /// fromSeconds から toSeconds までに動く変位。同じ時刻の差で出すので、フレームの刻みによらず合計は始点から終点までになる。
        /// </summary>
        public Vector3 Displacement(float fromSeconds, float toSeconds)
        {
            if (toSeconds < fromSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(toSeconds), toSeconds, "toSeconds must not be before fromSeconds");
            }

            return PositionAt(toSeconds) - PositionAt(fromSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
