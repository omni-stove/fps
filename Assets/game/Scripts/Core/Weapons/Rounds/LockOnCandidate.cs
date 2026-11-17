using System.Numerics;

namespace Shooter.Core
{
    public readonly struct LockOnCandidate
    {
        public LockOnCandidate(int id, Vector3 point)
        {
            Id = id;
            Point = point;
        }

        public int Id { get; }

        public Vector3 Point { get; }
    }
}
