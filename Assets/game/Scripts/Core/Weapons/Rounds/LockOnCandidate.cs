using System.Numerics;

namespace Shooter.Core
{
    public readonly struct LockOnCandidate
    {
        public LockOnCandidate(int id, Vector3 point, ILockOnDecoy decoy = null)
        {
            Id = id;
            Point = point;
            Decoy = decoy;
        }

        public int Id { get; }

        public Vector3 Point { get; }

        // 囮を出せない相手は null。
        public ILockOnDecoy Decoy { get; }
    }
}
