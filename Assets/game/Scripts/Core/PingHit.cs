using System.Numerics;

namespace Shooter.Core
{
    public readonly struct PingHit
    {
        private PingHit(bool hasTarget, PingKind kind, Vector3 position, int candidateIndex)
        {
            HasTarget = hasTarget;
            Kind = kind;
            Position = position;
            CandidateIndex = candidateIndex;
        }

        public static PingHit None => new PingHit(false, PingKind.Location, Vector3.Zero, -1);

        public bool HasTarget { get; }

        public PingKind Kind { get; }

        public Vector3 Position { get; }

        public int CandidateIndex { get; }

        public static PingHit Enemy(int candidateIndex, Vector3 position) => new PingHit(true, PingKind.Enemy, position, candidateIndex);

        public static PingHit Location(Vector3 position) => new PingHit(true, PingKind.Location, position, -1);
    }
}
