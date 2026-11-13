using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// シーンにある拠点の範囲 (<see cref="ControlPointArea"/>) の一覧。番号が 0 から抜けも重なりもなく並んでいることを生成時に確かめ、
    /// ある位置がどの拠点にいるかを <see cref="Locate"/> で引く。<see cref="Domination"/> の拠点の数は <see cref="Count"/> に合わせる。
    /// </summary>
    public sealed class ControlPointMap
    {
        private readonly ControlPointArea[] _areas;

        /// <summary>拠点の数。</summary>
        public int Count => _areas.Length;

        /// <summary>番号 index の拠点の範囲。</summary>
        public ControlPointArea this[int index]
        {
            get
            {
                if (index < 0 || index >= _areas.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "no control point with this index");
                }

                return _areas[index];
            }
        }

        /// <param name="areas">拠点の範囲。並びは問わないが、番号は 0 から Count - 1 まで 1 つずつあること。1 つ以上。</param>
        /// <exception cref="ArgumentException">空、番号が重なっている、または抜けている。</exception>
        public ControlPointMap(IReadOnlyList<ControlPointArea> areas)
        {
            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

            if (areas.Count == 0)
            {
                throw new ArgumentException("at least one control point is required", nameof(areas));
            }

            var sorted = new ControlPointArea[areas.Count];
            var filled = new bool[areas.Count];
            foreach (var area in areas)
            {
                if (area.Index >= sorted.Length)
                {
                    throw new ArgumentException(
                        $"control point index {area.Index} is out of range; indices must run from 0 to {sorted.Length - 1} without gaps",
                        nameof(areas));
                }

                if (filled[area.Index])
                {
                    throw new ArgumentException($"control point index {area.Index} is used more than once", nameof(areas));
                }

                sorted[area.Index] = area;
                filled[area.Index] = true;
            }

            _areas = sorted;
        }

        /// <summary>
        /// position がいる拠点の番号。どの範囲にもいなければ null。範囲が重なる所では、水平の距離が中心にいちばん近い拠点
        /// (同じなら番号の小さいほう) にいるとみなす。<see cref="Domination"/> は 1 人を 1 つの拠点にしか数えないため。
        /// </summary>
        public int? Locate(Vector3 position)
        {
            int? best = null;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < _areas.Length; i++)
            {
                var area = _areas[i];
                if (!area.Contains(position))
                {
                    continue;
                }

                var distance = area.HorizontalDistanceSquared(position);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
