using System.Numerics;

namespace Shooter.Core
{
    public readonly struct WorldHit
    {
        public WorldHit(Vector3 point, Vector3 normal, float distance, int colliderId, int layer)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
            ColliderId = colliderId;
            Layer = layer;
        }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public int ColliderId { get; }

        public int Layer { get; }
    }
}
