using System;

namespace Fps.Core
{
    /// <summary>物を等間隔で 1 列に並べるときの、列の中心からの横方向のずれ。</summary>
    public static class LineLayout
    {
        /// <summary>
        /// count 個を spacing 間隔で、中心が 0 になるよう左から順に並べたずれを返す。
        /// 例: 3 個・間隔 2 なら -2, 0, 2。count が 0 なら空。
        /// </summary>
        /// <param name="count">並べる数。0 以上。</param>
        /// <param name="spacing">隣との間隔。0 以上の有限値。</param>
        public static float[] CenteredOffsets(int count, float spacing)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            if (!(spacing >= 0f) || float.IsInfinity(spacing))
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "spacing must be a finite, non-negative value");
            }

            var offsets = new float[count];
            var first = -(count - 1) * spacing / 2f;
            for (var i = 0; i < count; i++)
            {
                offsets[i] = first + (i * spacing);
            }

            return offsets;
        }
    }
}
