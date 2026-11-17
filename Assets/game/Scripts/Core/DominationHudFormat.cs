using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の拠点の呼び名と、画面・ログ・通信で揃える切り捨てたポイント。拠点は番号の順に A, B, C ... と呼ぶ。
    /// チームのポイントは小数を持つが、画面とログには切り捨てた整数で出す。
    /// 拠点の呼び名はログやシーンの名前にも使うので、言語に依らない文字列のまま。HUD の文は UI (<c>DominationHudText</c>) が作る。
    /// </summary>
    public static class DominationHudFormat
    {
        private const int _letterCount = 26;

        /// <summary>
        /// 拠点の呼び名。0 から 25 は A から Z、それより後は <c>P27</c> のように 1 始まりの番号にする。
        /// </summary>
        /// <example><c>PointName(0)</c> は <c>A</c>、<c>PointName(26)</c> は <c>P27</c></example>
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

        /// <summary>画面とログに出すポイント。切り捨てた整数で、負・NaN は 0、int に収まらない値は int.MaxValue。</summary>
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
