using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の拠点 1 つの範囲。<see cref="Center"/> を中心に、水平は半径 <see cref="Radius"/>、
    /// 上下はそれぞれ <see cref="Height"/> の半分までの円柱の中を「拠点にいる」とみなす。
    /// </summary>
    public readonly struct ControlPointArea
    {
        /// <summary>拠点の番号。<see cref="Domination"/> の pointIndex と同じ。0 以上。</summary>
        public int Index { get; }

        /// <summary>範囲の中心。</summary>
        public Vector3 Center { get; }

        /// <summary>水平の半径 (m)。0 より大きい有限値。</summary>
        public float Radius { get; }

        /// <summary>円柱の高さ (m)。中心から上下に半分ずつ広がる。0 より大きい有限値。</summary>
        public float Height { get; }

        /// <param name="index">拠点の番号。0 以上。</param>
        /// <param name="center">範囲の中心。各成分は有限値。</param>
        /// <param name="radius">水平の半径 (m)。0 より大きい有限値。</param>
        /// <param name="height">円柱の高さ (m)。0 より大きい有限値。</param>
        public ControlPointArea(int index, Vector3 center, float radius, float height)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            if (!IsFinite(center))
            {
                throw new ArgumentOutOfRangeException(nameof(center), center, "center must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            Index = index;
            Center = center;
            Radius = radius;
            Height = height;
        }

        /// <summary>position が範囲の中 (境界を含む) なら true。NaN を含む位置は中に入れない。</summary>
        public bool Contains(Vector3 position)
        {
            if (!IsFinite(position))
            {
                return false;
            }

            return Math.Abs(position.Y - Center.Y) <= Height * 0.5f
                && HorizontalDistanceSquared(position) <= Radius * Radius;
        }

        /// <summary>中心から position までの、高さを無視した距離の 2 乗。</summary>
        public float HorizontalDistanceSquared(Vector3 position)
        {
            var dx = position.X - Center.X;
            var dz = position.Z - Center.Z;
            return (dx * dx) + (dz * dz);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
