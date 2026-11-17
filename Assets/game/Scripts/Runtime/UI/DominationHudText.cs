using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の HUD に出す文。表のキー <c>mode.domination.*</c> の <see cref="LocalizedText"/> で返す。
    /// 拠点の呼び名と切り捨てたポイントはログ・通信と揃えるので Core の <see cref="DominationHudFormat"/> を使う。
    /// </summary>
    public static class DominationHudText
    {
        /// <summary>中立の拠点の持ち主の欄に出す文。</summary>
        public static LocalizedText Neutral => new LocalizedText("mode.domination.neutral");

        /// <summary>チーム名と切り捨てたポイント。</summary>
        /// <example><c>TeamScore(Team.Alpha, 12.9f)</c> は <c>ALPHA 12</c></example>
        public static LocalizedText TeamScore(Team team, float score)
        {
            return HudFormat.TeamScore(team, DominationHudFormat.DisplayedScore(score));
        }

        /// <summary>拠点の呼び名と持ち主。中立なら <see cref="Neutral"/>。</summary>
        /// <example><c>PointOwner(0, Team.Bravo)</c> は <c>A BRAVO</c>、<c>PointOwner(1, null)</c> は <c>B --</c></example>
        public static LocalizedText PointOwner(int pointIndex, Team? owner)
        {
            return new LocalizedText(
                "mode.domination.pointOwner",
                DominationHudFormat.PointName(pointIndex),
                owner.HasValue ? HudFormat.TeamName(owner.Value) : Neutral);
        }
    }
}
