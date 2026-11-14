using System;
using System.Numerics;

namespace Fps.Core
{
    // Core は Unity を参照しないので、物理で調べるのは Runtime に任せ、ここは結果の値だけを持つ。
    public readonly struct DeploySurvey
    {
        // 四隅は、中心を通り足場の法線に垂直な面からのずれで受け取る。真上からの高さで受けると、置ける傾きの坂でも隅の高低差が段差に数えられてしまう。
        public DeploySurvey(Vector3 groundNormal, float lowestCorner, float highestCorner, bool allCornersGrounded, bool blocked)
        {
            var lengthSquared = groundNormal.LengthSquared();
            if (!(lengthSquared > 1e-12f) || float.IsInfinity(lengthSquared))
            {
                throw new ArgumentOutOfRangeException(nameof(groundNormal), groundNormal, "groundNormal must be non-zero and finite");
            }

            if (float.IsNaN(lowestCorner) || float.IsInfinity(lowestCorner))
            {
                throw new ArgumentOutOfRangeException(nameof(lowestCorner), lowestCorner, "lowestCorner must be finite");
            }

            if (float.IsNaN(highestCorner) || float.IsInfinity(highestCorner) || highestCorner < lowestCorner)
            {
                throw new ArgumentOutOfRangeException(nameof(highestCorner), highestCorner, "highestCorner must be finite and not below lowestCorner");
            }

            HasGround = true;
            GroundNormal = Vector3.Normalize(groundNormal);
            LowestCorner = lowestCorner;
            HighestCorner = highestCorner;
            AllCornersGrounded = allCornersGrounded;
            Blocked = blocked;
        }

        public static DeploySurvey NoGround => default;

        public bool HasGround { get; }

        public Vector3 GroundNormal { get; }

        public float LowestCorner { get; }

        public float HighestCorner { get; }

        public bool AllCornersGrounded { get; }

        public bool Blocked { get; }
    }
}
