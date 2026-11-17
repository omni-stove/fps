using System.Numerics;

namespace Shooter.Core
{
    public readonly struct VaultObstacleSample
    {
        public VaultObstacleSample(float distance, Vector3 normal, float topHeight, float depth, float width = float.PositiveInfinity)
        {
            HasHit = true;
            Distance = distance;
            Normal = normal;
            TopHeight = topHeight;
            Depth = depth;
            Width = width;
        }

        public static VaultObstacleSample None => default;

        public bool HasHit { get; }

        public float Distance { get; }

        public Vector3 Normal { get; }

        public float TopHeight { get; }

        public float Depth { get; }

        public float Width { get; }
    }
}
