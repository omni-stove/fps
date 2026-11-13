using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 最後に選んだロードアウト (スロット順の武器の Id) を <see cref="PlayerPrefs"/> の 1 つのキーに
    /// <see cref="LoadoutPreference.ToJson"/> の文字列で読み書きする。
    /// </summary>
    public sealed class LoadoutPreferenceStore
    {
        /// <summary>ゲームが使う PlayerPrefs のキー。</summary>
        public const string DefaultKey = "Fps.LoadoutChoice";

        public const string LogTag = "[Loadout]";

        /// <param name="key">PlayerPrefs のキー。テストでは本物の選択を壊さないよう別のキーを渡す。</param>
        public LoadoutPreferenceStore(string key = DefaultKey)
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
        /// 保存した並びを読む。まだ保存していなければ警告を出さずに null。壊れていれば理由を警告で出して null
        /// (次に選んで保存すれば上書きされる)。
        /// </summary>
        public IReadOnlyList<string> Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return null;
            }

            var ids = LoadoutPreference.FromJson(PlayerPrefs.GetString(Key, string.Empty), out var problem);
            if (ids == null)
            {
                Debug.LogWarning($"{LogTag} saved loadout '{Key}': {problem}; choosing from the match's loadout instead");
            }

            return ids;
        }

        /// <summary>weaponIds を保存し、すぐディスクに書き出す (次に起動しても残す)。</summary>
        /// <exception cref="ArgumentNullException">weaponIds が null。</exception>
        /// <exception cref="ArgumentException">空・空の Id がある。</exception>
        public void Save(IReadOnlyList<string> weaponIds)
        {
            PlayerPrefs.SetString(Key, LoadoutPreference.ToJson(weaponIds));
            PlayerPrefs.Save();
        }

        /// <summary>保存した並びを消す。次の <see cref="Load"/> は null を返す。</summary>
        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
