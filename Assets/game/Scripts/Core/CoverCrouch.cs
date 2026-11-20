using System;

namespace Shooter.Core
{
    public static class CoverCrouch
    {
        public const float DefaultHeadClearance = 0.05f;

        // 姿勢ごとの関節の曲げ方はリグによって違うので、頭の高さが 2 つの姿勢の間を直線で動くとみなした割合で渡し、姿勢の混ぜ方は見た目の側に任せる。
        public static float Amount(float wallHeight, float standingTop, float lowestTop, float headClearance)
        {
            RequireFiniteNotNegative(wallHeight, nameof(wallHeight));
            RequireFiniteNotNegative(lowestTop, nameof(lowestTop));
            RequireFiniteNotNegative(headClearance, nameof(headClearance));
            if (!(standingTop > lowestTop) || float.IsInfinity(standingTop))
            {
                throw new ArgumentOutOfRangeException(nameof(standingTop), standingTop, "standingTop must be finite and greater than lowestTop");
            }

            var target = wallHeight - headClearance;
            var amount = (standingTop - target) / (standingTop - lowestTop);
            return amount <= 0f ? 0f : amount >= 1f ? 1f : amount;
        }

        // 覗いている間は壁の上か横へ体を出すので、しゃがまない。
        public static float Amount(bool inCover, bool peeking, float wallHeight, float standingTop, float lowestTop, float headClearance)
        {
            return inCover && !peeking ? Amount(wallHeight, standingTop, lowestTop, headClearance) : 0f;
        }

        private static void RequireFiniteNotNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }
    }
}
