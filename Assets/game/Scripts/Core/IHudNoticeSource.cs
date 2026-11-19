using System;

namespace Shooter.Core
{
    // HudModel が CampaignNotice を名指ししないのは、キャンペーンを持たないゲームでも HUD の通知を他の出し手から受けられるようにするため。
    public interface IHudNoticeSource
    {
        HudNotice Notice { get; }

        event Action<HudNotice> Changed;
    }
}
