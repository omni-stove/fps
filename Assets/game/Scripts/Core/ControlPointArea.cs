using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct ControlPointArea
    {
        public int Index { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float Height { get; }

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

        public bool Contains(Vector3 position)
        {
            if (!IsFinite(position))
            {
                return false;
            }

            return Math.Abs(position.Y - Center.Y) <= Height * 0.5f
                && HorizontalDistanceSquared(position) <= Radius * Radius;
        }

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
