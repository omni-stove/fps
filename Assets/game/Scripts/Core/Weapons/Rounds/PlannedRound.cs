using System.Numerics;

namespace Shooter.Core
{
    public readonly struct PlannedRound
    {
        public PlannedRound(Vector3 origin, Vector3 direction, int index)
        {
            Origin = origin;
            Direction = direction;
            Index = index;
        }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }

        public int Index { get; }
    }
}
