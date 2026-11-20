using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // クラウドセーブの同期は個々のセーブの形を知らないので、レートの側からスロットと読める判定を渡す。
    public static class SkillCloudSave
    {
        public static void Register()
        {
            CloudSaveBootstrap.RegisterSlots(IsSlot, new[] { SkillRecorder.Slot }, CanRead);
        }

        public static bool IsSlot(string slot)
        {
            return string.Equals(slot, SkillRecorder.Slot, StringComparison.Ordinal);
        }

        public static bool CanRead(string text)
        {
            SkillSave.FromJson(text);
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
