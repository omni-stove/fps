using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SquadOrderCandidate
    {
        public SquadOrderCandidate(Side side, Vector3 position, bool isAlive)
        {
            Side = side;
            Position = position;
            IsAlive = isAlive;
        }

        public Side Side { get; }

        public Vector3 Position { get; }

        public bool IsAlive { get; }
    }
}
