using Shooter.Core;

namespace Shooter.Runtime
{
    // 拠点の呼び名はログ・通信と揃えるので、Core の DominationHudFormat を使う。
    public static class DominationHudText
    {
        public static LocalizedText Neutral => new LocalizedText("mode.domination.neutral");

        public static LocalizedText PointOwner(int pointIndex, Team? owner)
        {
            return new LocalizedText(
                "mode.domination.pointOwner",
                DominationHudFormat.PointName(pointIndex),
                owner.HasValue ? HudText.TeamName(owner.Value) : Neutral);
        }
    }
}
