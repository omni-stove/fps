using System.Numerics;

namespace Shooter.Core
{
    public readonly struct PingCandidate
    {
        public PingCandidate(Vector3 position, bool isAlive, bool isVisible)
        {
            Position = position;
            IsAlive = isAlive;
            IsVisible = isVisible;
        }

        public Vector3 Position { get; }

        public bool IsAlive { get; }

        public bool IsVisible { get; }
    }
}
