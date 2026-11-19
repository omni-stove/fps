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

        public static bool IsSlot(string slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (string.Equals(slot, Legacy, System.StringComparison.Ordinal))
            {
                return true;
            }

            return slot.StartsWith(Prefix, System.StringComparison.Ordinal)
                && CampaignLaunch.IsValidMissionId(slot.Substring(Prefix.Length));
        }
    }
}
