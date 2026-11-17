using System.Numerics;

namespace Shooter.Core
{
    public readonly struct CoverWallSample
    {
        public CoverWallSample(float distance, Vector3 normal, float topHeight, bool wallOnLeft, bool wallOnRight)
        {
            HasHit = true;
            Distance = distance;
            Normal = normal;
            TopHeight = topHeight;
            WallOnLeft = wallOnLeft;
            WallOnRight = wallOnRight;
        }

        public static CoverWallSample None => default;

        public bool HasHit { get; }

        public float Distance { get; }

        public Vector3 Normal { get; }

        public float TopHeight { get; }

        public bool WallOnLeft { get; }

        public bool WallOnRight { get; }
    }
}
