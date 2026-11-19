using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 本体のクラウドセーブはキャンペーンのセーブの形を知らないので、キャンペーンの側からスロットと読める判定を渡す。
    public static class CampaignCloudSave
    {
        public static void Register()
        {
            CloudSaveBootstrap.RegisterSlots(CampaignSaveSlots.IsSlot, new[] { CampaignSaveSlots.Legacy }, CanRead);
        }

        public static bool CanRead(string text)
        {
            CampaignSave.FromJson(text);
            return true;
        }

        // ProgressionBootstrap が BeforeSceneLoad で同期を作り、そのとき起動時のスロットを受け取るので、それより前に入れる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            Register();
        }
    }
}
