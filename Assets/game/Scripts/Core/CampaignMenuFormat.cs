using System;

namespace Fps.Core
{
    /// <summary>キャンペーンのメニュー (<see cref="CampaignMenu"/>) に出す文言。</summary>
    public static class CampaignMenuFormat
    {
        public const string Title = "CAMPAIGN";
        public const string DifficultyTitle = "DIFFICULTY";
        public const string SaveBadge = "SAVED";
        public const string Continue = "CONTINUE";
        public const string Restart = "START OVER";
        public const string Start = "START";
        public const string Back = "BACK";
        public const string ConfirmRestart = "DELETE SAVE AND START";
        public const string CancelRestart = "CANCEL";

        /// <summary>カタログが使えないときにメインメニューへ出す案内の頭。続けて理由を出す。</summary>
        public const string UnavailableNotice = "The campaign cannot be opened: ";

        /// <summary>難易度のボタンの文言。</summary>
        public static string DifficultyLabel(CampaignDifficulty difficulty)
        {
            return CampaignLaunchOptions.DifficultyName(difficulty).ToUpperInvariant();
        }

        /// <summary>「最初から」の確認の問い。</summary>
        public static string ConfirmRestartQuestion(string missionName)
        {
            return "Delete the saved progress of " + missionName + " and start from the beginning?";
        }

        /// <summary>セーブを消せなかったときの案内。</summary>
        public static string DeleteFailed(string reason)
        {
            return "Could not delete the save: " + reason;
        }

        /// <summary>ミッションのシーンが Build Settings に無いときの案内。</summary>
        public static string SceneMissing(string missionName)
        {
            return "Cannot open " + missionName + ": its scene is not in this build.";
        }

        /// <summary>カタログが不正などで始められなかったときの案内。</summary>
        public static string CannotStart(string reason)
        {
            return "Cannot start the mission: " + reason;
        }

        /// <summary>項目の文言。ミッションと難易度の行は別に描くので扱わない。</summary>
        public static string ItemLabel(CampaignMenuItemKind kind)
        {
            switch (kind)
            {
                case CampaignMenuItemKind.Continue:
                    return Continue;
                case CampaignMenuItemKind.Restart:
                    return Restart;
                case CampaignMenuItemKind.Start:
                    return Start;
                case CampaignMenuItemKind.Back:
                    return Back;
                case CampaignMenuItemKind.ConfirmRestart:
                    return ConfirmRestart;
                case CampaignMenuItemKind.CancelRestart:
                    return CancelRestart;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "the item has no fixed label");
            }
        }
    }
}
