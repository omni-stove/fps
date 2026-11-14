using System;
using System.Collections.Generic;

namespace Fps.Core.Navigation
{
    internal sealed class XzGrid
    {
        private const int _maxCellsPerAxis = 1024;

        private readonly float _minX;
        private readonly float _minZ;
        private readonly float _cellSize;
        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _cellStarts;
        private readonly int[] _items;
        private readonly int _itemCount;
        private readonly int[] _stamps;
        private int _stamp;

        public XzGrid(IReadOnlyList<(float MinX, float MinZ, float MaxX, float MaxZ)> boxes)
        {
            _itemCount = boxes.Count;
            _stamps = new int[_itemCount];
            if (_itemCount == 0)
            {
                _cellSize = 1f;
                _columns = 1;
                _rows = 1;
                _cellStarts = new int[2];
                _items = Array.Empty<int>();
                return;
            }

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, sizeSum = 0f;
            foreach (var box in boxes)
            {
                minX = Math.Min(minX, box.MinX);
                minZ = Math.Min(minZ, box.MinZ);
                maxX = Math.Max(maxX, box.MaxX);
                maxZ = Math.Max(maxZ, box.MaxZ);
                sizeSum += Math.Max(box.MaxX - box.MinX, box.MaxZ - box.MinZ);
            }

            // 1 項目がおおよそ 1 マスに収まる細かさにすると、引く項目もマスへの重複も少なくて済む。
            var cellSize = Math.Max(sizeSum / _itemCount, 1e-3f);
            var extent = Math.Max(maxX - minX, maxZ - minZ);
            cellSize = Math.Max(cellSize, extent / _maxCellsPerAxis);
            _minX = minX;
            _minZ = minZ;
            _cellSize = cellSize;
            _columns = Math.Max(1, (int)Math.Floor((maxX - minX) / cellSize) + 1);
            _rows = Math.Max(1, (int)Math.Floor((maxZ - minZ) / cellSize) + 1);

            var cellCount = _columns * _rows;
            var counts = new int[cellCount + 1];
            foreach (var box in boxes)
            {
                CellRange(box.MinX, box.MinZ, box.MaxX, box.MaxZ, out var c0, out var r0, out var c1, out var r1);
                for (var r = r0; r <= r1; r++)
                {
                    for (var c = c0; c <= c1; c++)
                    {
                        counts[r * _columns + c + 1]++;
                    }
                }
            }

            for (var i = 1; i <= cellCount; i++)
            {
                counts[i] += counts[i - 1];
            }

            _cellStarts = counts;
            _items = new int[counts[cellCount]];
            var fill = new int[cellCount];
            for (var i = 0; i < _itemCount; i++)
            {
                var box = boxes[i];
                CellRange(box.MinX, box.MinZ, box.MaxX, box.MaxZ, out var c0, out var r0, out var c1, out var r1);
                for (var r = r0; r <= r1; r++)
                {
                    for (var c = c0; c <= c1; c++)
                    {
                        var cell = r * _columns + c;
                        _items[_cellStarts[cell] + fill[cell]++] = i;
                    }
                }
            }
        }

        public void Query(float minX, float minZ, float maxX, float maxZ, List<int> results)
        {
            results.Clear();
            if (_itemCount == 0)
            {
                return;
            }

            if (++_stamp == int.MaxValue)
            {
                Array.Clear(_stamps, 0, _stamps.Length);
                _stamp = 1;
            }

            CellRange(minX, minZ, maxX, maxZ, out var c0, out var r0, out var c1, out var r1);
            if ((long)(c1 - c0 + 1) * (r1 - r0 + 1) >= _itemCount)
            {
                for (var i = 0; i < _itemCount; i++)
                {
                    results.Add(i);
                }

                return;
            }

            for (var r = r0; r <= r1; r++)
            {
                for (var c = c0; c <= c1; c++)
                {
                    var cell = r * _columns + c;
                    for (var k = _cellStarts[cell]; k < _cellStarts[cell + 1]; k++)
                    {
                        var item = _items[k];
                        if (_stamps[item] != _stamp)
                        {
                            _stamps[item] = _stamp;
                            results.Add(item);
                        }
                    }
                }
            }
        }

        private void CellRange(float minX, float minZ, float maxX, float maxZ, out int c0, out int r0, out int c1, out int r1)
        {
            c0 = Cell(minX, _minX, _columns);
            r0 = Cell(minZ, _minZ, _rows);
            c1 = Cell(maxX, _minX, _columns);
            r1 = Cell(maxZ, _minZ, _rows);
        }

        private int Cell(float value, float origin, int count)
        {
            var cell = (value - origin) / _cellSize;
            if (!(cell > 0f))
            {
                return 0;
            }

            return cell >= count - 1 ? count - 1 : (int)cell;
        }
    }
}
