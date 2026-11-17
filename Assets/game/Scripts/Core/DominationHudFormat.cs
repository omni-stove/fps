using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class DominationHudFormat
    {
        private const int _letterCount = 26;

        // 拠点の呼び名はログやシーンの名前にも使うので、言語に依らない文字列のままにする。
        public static string PointName(int pointIndex)
        {
            if (pointIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "pointIndex must not be negative");
            }

            return pointIndex < _letterCount
                ? ((char)('A' + pointIndex)).ToString()
                : "P" + (pointIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        public static int DisplayedScore(float score)
        {
            if (!(score > 0f))
            {
                return 0;
            }

            if (score >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Floor(score);
        }
    }
}
