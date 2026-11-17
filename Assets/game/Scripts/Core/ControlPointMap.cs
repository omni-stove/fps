using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class ControlPointMap
    {
        private readonly ControlPointArea[] _areas;

        public int Count => _areas.Length;

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

        // 範囲が重なる所でも 1 つの拠点だけを返すのは、Domination が 1 人を 1 つの拠点にしか数えないため。
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
