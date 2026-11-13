using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の HUD とログに出す文字列。拠点は番号の順に A, B, C ... と呼ぶ。
    /// チームのポイントは小数を持つが、画面とログには切り捨てた整数で出す。
    /// </summary>
    public static class DominationHudFormat
    {
        /// <summary>中立の拠点の持ち主の欄に出す文字列。</summary>
        public const string Neutral = "--";

        private const int LetterCount = 26;

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

            return pointIndex < LetterCount
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

        /// <summary>チーム名と切り捨てたポイント。</summary>
        /// <example><c>TeamScore(Team.Alpha, 12.9f)</c> は <c>ALPHA 12</c></example>
        public static string TeamScore(Team team, float score)
        {
            return HudFormat.TeamScore(team, DisplayedScore(score));
        }

        /// <summary>拠点の呼び名と持ち主。中立なら <see cref="Neutral"/>。</summary>
        /// <example><c>PointOwner(0, Team.Bravo)</c> は <c>A BRAVO</c>、<c>PointOwner(1, null)</c> は <c>B --</c></example>
        public static string PointOwner(int pointIndex, Team? owner)
        {
            return PointName(pointIndex) + " " + (owner.HasValue ? HudFormat.TeamName(owner.Value) : Neutral);
        }
    }
}
