using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class StashStore
    {
        public const string DefaultKey = "Shooter.Stash";

        public const string BrokenSuffix = ".broken";

        // キーを渡せるのは、テストで本物の持ち物庫を壊さないよう別のキーを使うため。
        public StashStore(string key = DefaultKey)
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

        public string BrokenKey => Key + BrokenSuffix;

        public bool HasSave => PlayerPrefs.HasKey(Key);

        public Stash Load(StashSpec spec, ItemCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!PlayerPrefs.HasKey(Key))
            {
                return Stash.Fresh(spec, catalog);
            }

            var text = PlayerPrefs.GetString(Key, string.Empty);
            var stacks = StashSave.FromJson(text, out var problem);
            if (stacks == null)
            {
                // 次の保存で読めなかった中身が上書きされると直す手が無くなるので、別のキーに写してから始め直す。
                PlayerPrefs.SetString(BrokenKey, text);
                PlayerPrefs.Save();
                Debug.LogWarning(StashLog.BrokenSave(Key, BrokenKey, problem));
                return Stash.Fresh(spec, catalog);
            }

            if (stacks.Count > spec.Capacity)
            {
                Debug.LogWarning(StashLog.OverCapacity(Key, stacks.Count, spec.Capacity));
            }

            return Stash.Restore(spec.Capacity, catalog, stacks);
        }

        public void Save(Stash stash)
        {
            if (stash == null)
            {
                throw new ArgumentNullException(nameof(stash));
            }

            PlayerPrefs.SetString(Key, StashSave.ToJson(stash.Stacks));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BrokenKey);
            PlayerPrefs.Save();
        }
    }
}
