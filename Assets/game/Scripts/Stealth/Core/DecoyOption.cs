using System.Numerics;

namespace Shooter.Core
{
    public readonly struct DecoyOption
    {
        public DecoyOption(bool isHostile, Vector3 position, bool isAlive, bool isSeen, bool isHeard)
        {
            IsHostile = isHostile;
            Position = position;
            IsAlive = isAlive;
            IsSeen = isSeen;
            IsHeard = isHeard;
        }

        public bool IsHostile { get; }

        public Vector3 Position { get; }

        public bool IsAlive { get; }

        public bool IsSeen { get; }

        public bool IsHeard { get; }
    }
}
