using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class CampaignMenuFormat
    {
        public static string Title => GameText.Get("campaign.title");
        public static string DifficultyTitle => GameText.Get("campaign.difficultyTitle");
        public static string SaveBadge => GameText.Get("campaign.saveBadge");
        public static string Continue => GameText.Get("campaign.continue");
        public static string Restart => GameText.Get("campaign.restart");
        public static string Start => GameText.Get("campaign.start");
        public static string Back => GameText.Get("campaign.back");
        public static string ConfirmRestart => GameText.Get("campaign.confirmRestart");
        public static string CancelRestart => GameText.Get("campaign.cancelRestart");

        public static string Unavailable(string reason)
        {
            return GameText.Format("campaign.unavailable", reason);
        }

        public static string DifficultyLabel(CampaignDifficulty difficulty)
        {
            // 定義済みかの確かめは DifficultyName に任せ、キーは言語に依らない名前から作る。
            return GameText.Get("campaign.difficulty." + CampaignLaunchOptions.DifficultyName(difficulty));
        }

        public static string ConfirmRestartQuestion(string missionName)
        {
            return GameText.Format("campaign.confirmRestartQuestion", missionName);
        }

        public static string DeleteFailed(string reason)
        {
            return GameText.Format("campaign.deleteFailed", reason);
        }

        public static string SceneMissing(string missionName)
        {
            return GameText.Format("campaign.sceneMissing", missionName);
        }

        public static string CannotStart(string reason)
        {
            return GameText.Format("campaign.cannotStart", reason);
        }

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
