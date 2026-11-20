namespace Shooter.Core
{
    public static class CampaignSaveSlots
    {
        public const string Prefix = "campaign-";

        public static string For(string missionId)
        {
            CampaignLaunch.RequireValidMissionId(missionId, nameof(missionId));
            return Prefix + missionId;
        }

        public static bool IsSlot(string slot)
        {
            if (slot == null)
            {
                return false;
            }

            return slot.StartsWith(Prefix, System.StringComparison.Ordinal)
                && CampaignLaunch.IsValidMissionId(slot.Substring(Prefix.Length));
        }
    }
}
