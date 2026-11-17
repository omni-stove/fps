using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ControlSettingsStore
    {
        public const string DefaultKey = "Shooter.ControlSettings";

        // キーを渡せるのは、テストで本物の設定を壊さないよう別のキーを使うため。
        public ControlSettingsStore(string key = DefaultKey)
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

        public string Key { get; }

        public ControlSettings Defaults => ControlSettings.Default;

        public ControlSettings Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return Defaults;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として初期値に戻る。
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var settings = ControlSettings.FromJson(text, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning($"[Input] control settings '{Key}': {problem}; using the default or the nearest valid value");
            }

            return settings;
        }

        public void Save(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetString(Key, settings.ToJson());
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
