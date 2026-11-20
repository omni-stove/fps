using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class ProgressionBootstrap
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static ProgressionRecorder _recorder;

        private static AchievementRecorder _achievements;

        private static CosmeticLocker _cosmetics;

        private static Func<string, bool> _gate;

        public static ProgressionRecorder Recorder => _recorder;

        public static AchievementRecorder Achievements => _achievements;

        public static CosmeticLocker Cosmetics => _cosmetics;

        public static ProgressionRecorder Install(ISaveStorage storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            return Install(new ProgressionRecorder(storage));
        }

        public static ProgressionRecorder Install(ProgressionRecorder recorder)
        {
            if (recorder == null)
            {
                throw new ArgumentNullException(nameof(recorder));
            }

            if (_recorder == recorder)
            {
                return recorder;
            }

            return Install(new AchievementRecorder(recorder)).Progression;
        }

        public static AchievementRecorder Install(AchievementRecorder achievements)
        {
            if (achievements == null)
            {
                throw new ArgumentNullException(nameof(achievements));
            }

            if (_achievements == achievements)
            {
                return achievements;
            }

            Uninstall();
            var recorder = achievements.Progression;
            recorder.Install();
            achievements.Install();
            _recorder = recorder;
            _achievements = achievements;
            var cosmetics = new CosmeticLocker(recorder);
            cosmetics.Install();
            _cosmetics = cosmetics;

            // 答えを覚えずに記録係の判定をそのまま門にするのは、試合中にレベルが上がればその場で使えるようにするため。
            Func<string, bool> gate = recorder.IsUnlocked;
            Unlocks.Install(gate);
            _gate = gate;
            return achievements;
        }

        public static void Uninstall()
        {
            var recorder = _recorder;
            var achievements = _achievements;
            var cosmetics = _cosmetics;
            var gate = _gate;
            _recorder = null;
            _achievements = null;
            _cosmetics = null;
            _gate = null;
            Unlocks.Uninstall(gate);
            cosmetics?.Uninstall();
            achievements?.Uninstall();
            recorder?.Uninstall();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // CombatEvents も同じ時点で購読を空にするので、前の Play の記録係は外すだけで手放せる。
            Uninstall();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallOnPlay()
        {
            Install(CloudSaveBootstrap.Storage);
            var sync = CloudSaveBootstrap.Current;
            if (sync != null)
            {
                sync.SlotPulled -= ReloadPulledProfile;
                sync.SlotPulled += ReloadPulledProfile;
            }

            // 次の Play まで残すと、その間に走らせた Edit Mode のテストが本物のプロフィールで判定され、保存先にも書き込むので、Play を抜けるときに外す。
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }

        internal static void ReloadPulledProfile(string slot)
        {
            if (string.Equals(slot, ProfileStore.Slot, StringComparison.Ordinal))
            {
                _recorder?.Reload();
            }
        }
    }
}
