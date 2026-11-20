using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 本体のクラウドセーブはキャンペーンのセーブの形を知らないので、キャンペーンの側からスロットと読める判定を渡す。
    public static class CampaignCloudSave
    {
        public static void Register()
        {
            CloudSaveBootstrap.RegisterSlots(CampaignSaveSlots.IsSlot, System.Array.Empty<string>(), CanRead);
        }

        public static bool CanRead(string text)
        {
            CampaignSave.FromJson(text);
            return true;
        }

        // 同期は CloudSaveBootstrap.Storage を最初に引いたとき (BeforeSceneLoad 以降) に作られ、そのとき起動時のスロットを受け取るので、それより前に入れる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            Register();
        }
    }
}
