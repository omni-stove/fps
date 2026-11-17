namespace Shooter.Core
{
    public static class CampaignSaveSlots
    {
        // ミッションごとのスロットにする前のセーブを読めるよう、そのスロットを LegacyMissionId のものとして読む。
        public const string Legacy = "campaign";

        public const string LegacyMissionId = "Campaign01";

        public const string Prefix = "campaign-";

        public static string For(string missionId)
        {
            CampaignLaunch.RequireValidMissionId(missionId, nameof(missionId));
            return Prefix + missionId;
        }

        public static bool ReadsLegacy(string missionId)
        {
            return string.Equals(missionId, LegacyMissionId, System.StringComparison.Ordinal);
        }
    }
}
