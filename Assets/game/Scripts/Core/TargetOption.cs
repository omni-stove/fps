using System.Numerics;

namespace Fps.Core
{
    public readonly struct TargetOption
    {
        public TargetOption(bool isHostile, Vector3 position, bool isAlive)
        {
            IsHostile = isHostile;
            Position = position;
            IsAlive = isAlive;
        }

        public bool IsHostile { get; }

        public Vector3 Position { get; }

        public bool IsAlive { get; }
    }
}
