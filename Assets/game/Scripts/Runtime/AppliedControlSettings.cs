using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class AppliedControlSettings
    {
        public const string LogTag = "[Settings]";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static ControlSettings _current;

        public static event Action<ControlSettings> Changed;

        public static ISettingsSink Sink { get; } = new AppliedSink();

        public static ControlSettings Current
        {
            get
            {
                if (_current == null)
                {
                    Apply(new ControlSettingsStore().Load());
                }

                return _current;
            }
        }

        public static void Apply(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var previous = _current;
            _current = settings;
            AudioListener.volume = settings.MasterVolume;
            ApplyPresentation(settings);
            if (settings.Equals(previous))
            {
                return;
            }

            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<ControlSettings>)handler)(settings);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} a settings change handler threw: {e.Message}");
                    Debug.LogException(e);
                }
            }
        }

        // 演出の倍率と血の表示は、プレイヤーが作り直されても保たれるよう各演出が全体で持つので、Changed の購読ではなくここで入れる。
        private static void ApplyPresentation(ControlSettings settings)
        {
            CameraShake.GlobalMultiplier = settings.CameraShakeIntensity;
            ViewmodelMotion.GlobalMultiplier = settings.ViewmodelMotionIntensity;
            BloodPresenter.BloodEnabled = settings.ShowBlood;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _current = null;
            Changed = null;
        }

        // メニューで設定画面を開く前から、保存した音量で鳴らす。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnPlay()
        {
            _ = Current;
        }

        private sealed class AppliedSink : ISettingsSink
        {
            public ControlSettings Current => AppliedControlSettings.Current;

            public void Apply(ControlSettings settings)
            {
                AppliedControlSettings.Apply(settings);
            }
        }
    }
}
