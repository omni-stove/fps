using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 本体のクラウドセーブはプロフィールの形を知らないので、進行の側からスロットと読める判定を渡す。
    public static class ProgressionCloudSave
    {
        public static void Register()
        {
            CloudSaveBootstrap.RegisterSlots(IsSlot, new[] { ProfileStore.Slot }, CanRead);
        }

        public static bool IsSlot(string slot)
        {
            return string.Equals(slot, ProfileStore.Slot, StringComparison.Ordinal);
        }

        public static bool CanRead(string text)
        {
            PlayerProfile.FromJson(text);
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
