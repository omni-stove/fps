using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 操作設定 (ControlSettings) と別のキーにするのは、言語は game 側の UI の設定で、キットの操作設定は言語を持たないため
    public sealed class LanguagePreferenceStore
    {
        public const string DefaultKey = "Shooter.Language";

        private readonly GameLanguage? _defaultLanguage;

        public LanguagePreferenceStore(
            string key = DefaultKey,
            string legacyKey = ControlSettingsStore.DefaultKey,
            GameLanguage? defaultLanguage = null)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0)
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            if (defaultLanguage.HasValue)
            {
                _ = GameLanguages.Code(defaultLanguage.Value);
            }

            Key = key;
            LegacyKey = legacyKey;

            // Application.systemLanguage は MonoBehaviour のフィールド初期化子から呼ぶと UnityException になるため、最初に使うときまで決めない。
            _defaultLanguage = defaultLanguage;
        }

        public string Key { get; }

        public string LegacyKey { get; }

        public bool HasSaved => PlayerPrefs.HasKey(Key);

        public GameLanguage Load()
        {
            Localizer.EnsureLanguageList();
            if (PlayerPrefs.HasKey(Key))
            {
                var code = PlayerPrefs.GetString(Key, string.Empty);
                if (GameLanguages.TryParseCode(code, out var saved))
                {
                    return saved;
                }

                var fallback = DefaultLanguage;
                Debug.LogWarning($"{Localizer.LogTag} language '{Key}': '{code}' is not a known language code; using {GameLanguages.Code(fallback)}");
                return fallback;
            }

            // 前の形式の保存は、操作設定を次に保存したときに言語を落とすので、読んだらすぐ自分のキーに書き移す。
            if (TryReadLegacy(out var legacy))
            {
                Save(legacy);
                return legacy;
            }

            return DefaultLanguage;
        }

        public void Save(GameLanguage language)
        {
            PlayerPrefs.SetString(Key, GameLanguages.Code(language));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private GameLanguage DefaultLanguage => _defaultLanguage ?? Localizer.SystemDefault();

        private bool TryReadLegacy(out GameLanguage language)
        {
            language = default;
            if (string.IsNullOrEmpty(LegacyKey) || !PlayerPrefs.HasKey(LegacyKey))
            {
                return false;
            }

            LegacySettings saved;
            try
            {
                saved = JsonUtility.FromJson<LegacySettings>(PlayerPrefs.GetString(LegacyKey, string.Empty));
            }
            catch (ArgumentException)
            {
                return false;
            }

            return saved != null && saved.language != null && GameLanguages.TryParseCode(saved.language, out language);
        }

        // 操作設定の形式 6・7 は、言語を "language" に言語コードで持っていた。ほかの項目は読まない。
        [Serializable]
        private sealed class LegacySettings
        {
            public string language = null;
        }
    }
}
