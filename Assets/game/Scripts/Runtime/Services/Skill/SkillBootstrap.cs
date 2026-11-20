using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class SkillBootstrap
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static SkillRecorder _current;

        public static SkillRecorder Current => _current;

        public static SkillRecorder Install(ISaveStorage storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            return Install(new SkillRecorder(storage));
        }

        public static SkillRecorder Install(SkillRecorder recorder)
        {
            if (recorder == null)
            {
                throw new ArgumentNullException(nameof(recorder));
            }

            if (_current == recorder)
            {
                return recorder;
            }

            Uninstall();
            recorder.Install();
            _current = recorder;
            return recorder;
        }

        public static void Uninstall()
        {
            var recorder = _current;
            _current = null;
            recorder?.Uninstall();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallOnPlay()
        {
            Install(CloudSaveBootstrap.Storage);
            var sync = CloudSaveBootstrap.Current;
            if (sync != null)
            {
                sync.SlotPulled -= ReloadPulledSkill;
                sync.SlotPulled += ReloadPulledSkill;
            }

            // 次の Play まで残すと、その間に走らせた Edit Mode のテストが本物のレートを見て、保存先にも書き込むので、Play を抜けるときに外す。
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }

        internal static void ReloadPulledSkill(string slot)
        {
            if (string.Equals(slot, SkillRecorder.Slot, StringComparison.Ordinal))
            {
                _current?.Reload();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // CombatEvents も同じ時点で購読を空にするので、前の Play の記録係は外すだけで手放せる。
            Uninstall();
        }
    }
}
