using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct CoverPoint
    {
        private const float _minFacingLength = 1e-6f;

        public CoverPoint(Vector3 position, Vector3 facing, CoverHeight height)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!IsFinite(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must be finite");
            }

            var flat = new Vector3(facing.X, 0f, facing.Z);
            var length = flat.Length();
            if (!(length > _minFacingLength))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must have a horizontal component");
            }

            Position = position;
            Facing = flat / length;
            Height = height;
        }

        public Vector3 Position { get; }

        public Vector3 Facing { get; }

        public CoverHeight Height { get; }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
