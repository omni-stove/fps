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

        public static ProgressionRecorder Recorder => _recorder;

        public static AchievementRecorder Achievements => _achievements;

        public static CosmeticLocker Cosmetics => _cosmetics;

        // 値を覚えずに呼ぶたびに今のプロフィールを引くのは、試合中にレベルが上がればその場で使えるようにするため。
        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            return _recorder == null || _recorder.IsUnlocked(id);
        }

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
            return achievements;
        }

        public static void Uninstall()
        {
            var recorder = _recorder;
            var achievements = _achievements;
            var cosmetics = _cosmetics;
            _recorder = null;
            _achievements = null;
            _cosmetics = null;
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
            Install(FileSaveStorage.CreateDefault());
            // 次の Play まで残すと、その間に走らせた Edit Mode のテストが本物のプロフィールで判定され、保存先にも書き込むので、Play を抜けるときに外す。
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
        }
    }
}
