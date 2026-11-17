using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class VoiceSettingsStore
    {
        public const string DefaultKey = "Shooter.VoiceSettings";

        // キーを渡せるのは、テストで本物の設定を壊さないよう別のキーを使うため。
        public VoiceSettingsStore(string key = DefaultKey)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0)
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            Key = key;
        }

        public static event Action<string, VoiceSettings> Saved;

        public string Key { get; }

        public VoiceSettings Defaults => VoiceSettings.Default;

        public VoiceSettings Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return Defaults;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として初期値に戻る
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var settings = VoiceSettings.FromJson(text, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning(VoiceSettingsLog.Problem(Key, problem));
            }

            return settings;
        }

        public void Save(VoiceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetString(Key, settings.ToJson());
            PlayerPrefs.Save();
            Saved?.Invoke(Key, settings);
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Saved = null;
        }
    }
}
