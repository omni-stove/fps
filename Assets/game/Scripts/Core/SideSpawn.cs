using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SideSpawn
    {
        public Side Side { get; }

        public Vector3 Position { get; }

        public SideSpawn(Side side, Vector3 position)
        {
            if (!HostileSelector.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Side = side;
            Position = position;
        }
    }
}
