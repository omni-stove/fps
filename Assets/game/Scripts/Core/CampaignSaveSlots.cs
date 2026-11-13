namespace Fps.Core
{
    /// <summary>
    /// キャンペーンのセーブのスロット名。ミッションごとに <see cref="Prefix"/> + ミッション Id のスロットに置く。
    /// ミッションが 1 つだった頃のスロット <see cref="Legacy"/> は <see cref="LegacyMissionId"/> のものとして読む。
    /// </summary>
    public static class CampaignSaveSlots
    {
        /// <summary>ミッションごとのスロットにする前に、唯一のミッションのセーブを置いていたスロット。</summary>
        public const string Legacy = "campaign";

        /// <summary><see cref="Legacy"/> のスロットを持っていたミッションの Id。</summary>
        public const string LegacyMissionId = "Campaign01";

        public const string Prefix = "campaign-";

        /// <summary>missionId のミッションのセーブを置くスロット。missionId が不正なら <see cref="System.ArgumentException"/>。</summary>
        public static string For(string missionId)
        {
            CampaignLaunch.RequireValidMissionId(missionId, nameof(missionId));
            return Prefix + missionId;
        }

        /// <summary>missionId のミッションが、セーブを探すときに <see cref="Legacy"/> のスロットも読むか。</summary>
        public static bool ReadsLegacy(string missionId)
        {
            return string.Equals(missionId, LegacyMissionId, System.StringComparison.Ordinal);
        }
    }
}
