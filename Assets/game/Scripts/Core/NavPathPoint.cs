using System.Numerics;

namespace Shooter.Core
{
    public readonly struct NavPathPoint
    {
        public const int NoLink = -1;

        public NavPathPoint(Vector3 position, int linkId)
        {
            Position = position;
            LinkId = linkId < 0 ? NoLink : linkId;
        }

        public Vector3 Position { get; }

        public int LinkId { get; }

        public bool StartsLink => LinkId != NoLink;
    }
}
