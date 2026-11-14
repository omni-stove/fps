using System;

namespace Fps.Core
{
    /// <summary>キャンペーンのメニュー (<see cref="CampaignMenu"/>) に出す文言。<see cref="GameText"/> から今の言語で引く。</summary>
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

        /// <summary>カタログが使えないときにメインメニューへ出す案内。reason に理由を入れる。</summary>
        public static string Unavailable(string reason)
        {
            return GameText.Format("campaign.unavailable", reason);
        }

        /// <summary>難易度のボタンの文言。</summary>
        /// <exception cref="ArgumentOutOfRangeException">difficulty が定義済みの値でない。</exception>
        public static string DifficultyLabel(CampaignDifficulty difficulty)
        {
            // 定義済みかの確かめは DifficultyName に任せ、キーは言語に依らない名前から作る。
            return GameText.Get("campaign.difficulty." + CampaignLaunchOptions.DifficultyName(difficulty));
        }

        /// <summary>「最初から」の確認の問い。</summary>
        public static string ConfirmRestartQuestion(string missionName)
        {
            return GameText.Format("campaign.confirmRestartQuestion", missionName);
        }

        /// <summary>セーブを消せなかったときの案内。</summary>
        public static string DeleteFailed(string reason)
        {
            return GameText.Format("campaign.deleteFailed", reason);
        }

        /// <summary>ミッションのシーンが Build Settings に無いときの案内。</summary>
        public static string SceneMissing(string missionName)
        {
            return GameText.Format("campaign.sceneMissing", missionName);
        }

        /// <summary>カタログが不正などで始められなかったときの案内。</summary>
        public static string CannotStart(string reason)
        {
            return GameText.Format("campaign.cannotStart", reason);
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
