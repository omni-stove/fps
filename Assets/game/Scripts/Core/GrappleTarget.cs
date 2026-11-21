using System.Numerics;

namespace Shooter.Core
{
    public readonly struct GrappleTarget
    {
        public GrappleTarget(int colliderId, int layer, Vector3 point, Vector3 normal, float distance, bool snappedToEdge)
        {
            ColliderId = colliderId;
            Layer = layer;
            Point = point;
            Normal = normal;
            Distance = distance;
            SnappedToEdge = snappedToEdge;
        }

        public int ColliderId { get; }

        public int Layer { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public bool SnappedToEdge { get; }

        public bool IsWall => GrappleSpec.IsWall(Normal);

        public bool IsFloor => Normal.LengthSquared() >= 1e-8f && Vector3.Normalize(Normal).Y >= GrappleSpec.FloorNormalY;
    }
}
