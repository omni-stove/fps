using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 今効いている操作設定。Play の開始時に <see cref="ControlSettingsStore"/> から読み、全体の音量を <see cref="AudioListener.volume"/> に入れる。
    /// 設定画面 (<see cref="SettingsView"/>) が <see cref="Apply"/> で変えると <see cref="Changed"/> で配り、
    /// <see cref="FirstPersonLook"/>・<see cref="AimDownSights"/>・<see cref="ViewRig"/>・<see cref="PlayerController"/> などが
    /// その場で使い直す。効果音を鳴らす側は <see cref="ControlSettings.EffectsVolume"/> を音量に掛ける。
    /// 保存はしない (保存は設定画面を閉じたときに <see cref="ControlSettingsStore.Save"/> で行う)。
    /// </summary>
    public static class AppliedControlSettings
    {
        public const string LogTag = "[Settings]";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static ControlSettings _current;

        /// <summary>
        /// 設定が変わったとき (<see cref="Apply"/> で違う値を入れたとき)。購読した側は受け取った設定をすぐ使い直す。
        /// 1 つの購読先が投げても、ほかの購読先には届ける。
        /// </summary>
        public static event Action<ControlSettings> Changed;

        /// <summary>今効いている設定。まだ読んでいなければ <see cref="ControlSettingsStore"/> (既定のキー) から読む。</summary>
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

        /// <summary>
        /// settings を今効いている設定にし、全体の音量を <see cref="AudioListener.volume"/> に入れる。
        /// 前と違う値なら <see cref="Changed"/> を呼ぶ。
        /// </summary>
        public static void Apply(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var previous = _current;
            _current = settings;
            AudioListener.volume = settings.MasterVolume;
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
    }
}
