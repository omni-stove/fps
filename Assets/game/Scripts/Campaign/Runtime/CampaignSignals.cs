using Shooter.Core;

namespace Shooter.Runtime
{
    public static class CampaignSignals
    {
        // GameSignals のプロパティにせず Create で作るのは、キャンペーンを持たないゲームで本体の一覧に残さず、それでも ResetAll で空に戻すため。
        public static GameSignal<TutorialStatus?> Tutorial { get; } = GameSignals.Create<TutorialStatus?>("Tutorial");

        public static GameSignal<CampaignNotice> Notice { get; } = GameSignals.Create<CampaignNotice>("CampaignNotice");
    }
}
