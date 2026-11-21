using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class GraphicsSettingsStore
    {
        public const string DefaultKey = "Shooter.GraphicsSettings";

        private readonly GraphicsSettings _defaults;

        // キーを渡せるのは、テストで本物の設定を壊さないよう別のキーを使うため。
        // 既定値を渡せるのは、テストが Resources/Tuning/graphics や GameContent の中身に左右されないようにするため。
        public GraphicsSettingsStore(string key = DefaultKey, GraphicsSettings defaults = null)
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
            _defaults = defaults;
        }

        public static event Action<string, GraphicsSettings> Saved;

        public string Key { get; }

        public GraphicsSettings Defaults => _defaults ?? KitTuning.Graphics.Defaults;

        public GraphicsSettings Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return Defaults;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として初期値に戻る。
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var settings = GraphicsSettings.FromJson(text, Defaults, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning(GraphicsSettingsLog.Problem(Key, problem));
            }

            return settings;
        }

        public void Save(GraphicsSettings settings)
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
