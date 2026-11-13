using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="ControlSettings"/> を <see cref="PlayerPrefs"/> の 1 つのキーに JSON の文字列 (<see cref="ControlSettings.ToJson"/>) で読み書きする。
    /// 読めなかった所は初期値に戻し、理由を <c>[Input]</c> 付きの警告で出す。
    /// </summary>
    public sealed class ControlSettingsStore
    {
        /// <summary>ゲームが使う PlayerPrefs のキー。</summary>
        public const string DefaultKey = "Fps.ControlSettings";

        /// <param name="key">PlayerPrefs のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
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

        /// <summary>読み書きする PlayerPrefs のキー。</summary>
        public string Key { get; }

        /// <summary>
        /// 保存した設定を読む。まだ保存していなければ警告を出さずに <see cref="ControlSettings.Default"/> を返す。
        /// 壊れた値や欠けた値はその所だけ初期値にし、理由ごとに警告を出す。
        /// </summary>
        public ControlSettings Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return ControlSettings.Default;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として初期値に戻る。
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var settings = ControlSettings.FromJson(text, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning($"[Input] control settings '{Key}': {problem}; using the default for it");
            }

            return settings;
        }

        /// <summary>settings を保存し、すぐディスクに書き出す。</summary>
        public void Save(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetString(Key, settings.ToJson());
            PlayerPrefs.Save();
        }

        /// <summary>保存した設定を消す。次の <see cref="Load"/> は初期値を返す。</summary>
        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
