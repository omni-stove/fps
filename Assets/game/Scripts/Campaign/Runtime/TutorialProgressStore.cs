using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class TutorialProgressStore
    {
        public const string DefaultKey = "Shooter.TutorialCompleted";

        // キーを渡せるのは、テストで本物の記録を壊さないよう別のキーを使うため。
        public TutorialProgressStore(string key = DefaultKey)
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

        public bool IsCompleted => PlayerPrefs.GetInt(Key, 0) == 1;

        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
