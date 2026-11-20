using System.Numerics;

namespace Shooter.Core
{
    public readonly struct CoverProbeResult
    {
        public CoverProbeResult(bool isWall, bool canEnter, CoverHeight height, CoverEdge edges, Vector3 normal, float distance, float topHeight)
        {
            IsWall = isWall;
            CanEnter = canEnter;
            Height = height;
            Edges = edges;
            Normal = normal;
            Distance = distance;
            TopHeight = topHeight;
        }

        public static CoverProbeResult None => default;

        public bool IsWall { get; }

        public bool CanEnter { get; }

        public CoverHeight Height { get; }

        public CoverEdge Edges { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public float TopHeight { get; }

        public bool IsAtLeftEdge => (Edges & CoverEdge.Left) != 0;

        public bool IsAtRightEdge => (Edges & CoverEdge.Right) != 0;
    }
}
