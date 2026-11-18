using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class BlockListStore
    {
        public const string DefaultKey = "Shooter.BlockList";

        // キーを渡せるのは、テストで本物の一覧を壊さないよう別のキーを使うため。
        public BlockListStore(string key = DefaultKey)
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

        public BlockList Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return new BlockList();
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として空の一覧になる
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var list = BlockList.FromJson(text, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning(BlockListLog.Problem(Key, problem));
            }

            return list;
        }

        public void Save(BlockList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            PlayerPrefs.SetString(Key, list.ToJson());
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
