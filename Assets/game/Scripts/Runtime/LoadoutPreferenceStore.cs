using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class LoadoutPreferenceStore
    {
        public const string DefaultKey = "Shooter.LoadoutChoice";

        public const string LogTag = "[Loadout]";

        // キーを渡せるのは、テストで本物の選択を壊さないよう別のキーを使うため。
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

        public string Key { get; }

        // 能力を保存していない (前の版の) 選択は AbilityIds が null で返る
        public LoadoutChoice Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return null;
            }

            var choice = LoadoutPreference.FromJson(PlayerPrefs.GetString(Key, string.Empty), out var problem);
            if (choice == null)
            {
                Debug.LogWarning($"{LogTag} saved loadout '{Key}': {problem}; choosing from the match's loadout instead");
            }

            return choice;
        }

        public void Save(LoadoutChoice choice)
        {
            PlayerPrefs.SetString(Key, LoadoutPreference.ToJson(choice));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
