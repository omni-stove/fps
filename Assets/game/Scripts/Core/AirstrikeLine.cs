using System;
using System.Numerics;

namespace Shooter.Core
{
    // オフラインの AirstrikeReward と専用サーバーが同じ位置に落とせるよう、並べ方を 1 か所に置く。
    public static class AirstrikeLine
    {
        private const float _radiansPerDegree = MathF.PI / 180f;

        // ミニマップの UI がそのまま矢印を回せるよう、MinimapProjection.TryHeadingDegrees と同じく +Z を 0 度、+X を 90 度とする。
        public static Vector3 Direction(float headingDegrees)
        {
            var radians = MinimapContact.NormalizeHeading(headingDegrees, nameof(headingDegrees)) * _radiansPerDegree;
            return new Vector3(MathF.Sin(radians), 0f, MathF.Cos(radians));
        }

        // 機体は向きの方へ飛ぶので、先に落ちる弾 (index 0) を線の後ろの端に置く。
        public static Vector3 PointOnLine(Vector3 center, float headingDegrees, float length, int index, int count)
        {
            MinimapContact.RequireFinite(center, nameof(center));
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "length must be non-negative and finite");
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be at least 1");
            }

            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be between 0 and count - 1");
            }

            var direction = Direction(headingDegrees);
            if (count == 1)
            {
                return center;
            }

            var along = ((index / (float)(count - 1)) - 0.5f) * length;
            return center + (direction * along);
        }

        public static Vector3 Scatter(Vector3 center, float radius, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be non-negative and finite");
            }

            // 半径を一様に取ると中心に寄るので、面積で一様になるよう平方根を取る。
            var distance = radius * Math.Sqrt(random.NextDouble());
            var angle = random.NextDouble() * 2.0 * Math.PI;
            return center + new Vector3((float)(distance * Math.Cos(angle)), 0f, (float)(distance * Math.Sin(angle)));
        }

        public static Vector3 ShellPoint(Vector3 center, float headingDegrees, StreakAirstrikeSpec spec, int index, Random random)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var onLine = PointOnLine(center, headingDegrees, spec.LineLength, index, spec.ShellCount);
            return spec.Spread > 0f ? Scatter(onLine, spec.Spread, random) : onLine;
        }

        public static bool IsWithinRange(Vector3 origin, Vector3 target, float range)
        {
            var dx = target.X - origin.X;
            var dz = target.Z - origin.Z;
            return (dx * dx) + (dz * dz) <= range * range;
        }
    }
}
