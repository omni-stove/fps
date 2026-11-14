using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 選んだ言語を <see cref="PlayerPrefs"/> に読み書きする。
    /// 言語は <see cref="ControlSettings.Language"/> として <see cref="ControlSettingsStore"/> と同じキーに入れ、ほかの設定の値は変えない。
    /// </summary>
    public sealed class LanguagePreferenceStore
    {
        // 言語だけを別のキーに置くと、設定画面が保存する ControlSettings の言語と食い違うため、同じキーに書く。
        private readonly ControlSettingsStore _settings;

        /// <param name="key">PlayerPrefs のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
        /// <param name="defaultLanguage">保存した言語が無いときの言語。null なら OS の言語 (<see cref="Localizer.SystemDefault"/>)。</param>
        /// <exception cref="ArgumentNullException">key が null。</exception>
        /// <exception cref="ArgumentException">key が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">defaultLanguage が定義済みの値でない。</exception>
        public LanguagePreferenceStore(string key = ControlSettingsStore.DefaultKey, GameLanguage? defaultLanguage = null)
        {
            _settings = new ControlSettingsStore(key, defaultLanguage);
        }

        /// <summary>読み書きする PlayerPrefs のキー。</summary>
        public string Key => _settings.Key;

        /// <summary>言語を保存したことがあるか。</summary>
        public bool HasSaved => PlayerPrefs.HasKey(Key);

        /// <summary>保存した言語。まだ保存していなければ初期値の言語。壊れた値は <see cref="ControlSettingsStore.Load"/> と同じく警告を出して初期値に戻す。</summary>
        public GameLanguage Load()
        {
            return _settings.Load().Language;
        }

        /// <summary>language を保存し、すぐディスクに書き出す。言語のほかの保存した設定はそのまま残す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public void Save(GameLanguage language)
        {
            _ = GameLanguages.Code(language);
            _settings.Save(_settings.Load().WithLanguage(language));
        }
    }
}
