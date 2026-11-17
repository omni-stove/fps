using System;

namespace Shooter.Core
{
    public static class LineLayout
    {
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
