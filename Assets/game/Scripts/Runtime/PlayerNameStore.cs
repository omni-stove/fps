using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 名前はほかの人の画面に出るので、PlayerDisplayName を通った値だけを保存し、読むときにも同じ決まりで確かめる
    public sealed class PlayerNameStore
    {
        public const string DefaultKey = "Shooter.PlayerName";

        // キーを渡せるのは、テストで本物の名前を壊さないよう別のキーを使うため。
        // 長さを渡せるのは、試合の設定 (NetMatchSettings.MaxDisplayNameLength) を変えたゲームがその上限で保存するため
        public PlayerNameStore(string key = DefaultKey, int maxLength = NetMatchSettings.DefaultMaxDisplayNameLength)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0)
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            if (maxLength < 1 || maxLength > NetMatchSettings.MaxMaxDisplayNameLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLength), maxLength, nameof(maxLength) + " must be between 1 and " + NetMatchSettings.MaxMaxDisplayNameLength);
            }

            Key = key;
            MaxLength = maxLength;
        }

        public static event Action<string, string> Saved;

        public string Key { get; }

        public int MaxLength { get; }

        // 保存していないときと壊れているときは空を返す。空なら呼ぶ側が P<id> などの名前に戻す
        public string Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return string.Empty;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として空に戻る
            var text = PlayerPrefs.GetString(Key, string.Empty);
            if (!PlayerDisplayName.IsValid(text, MaxLength))
            {
                Debug.LogWarning(PlayerNameLog.Rejected(Key, MaxLength));
                return string.Empty;
            }

            return text;
        }

        // 空か空白だけなら保存した名前を消して true を返す。決まりに合わない名前は保存せず false を返す
        public bool TrySave(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Delete();
                Saved?.Invoke(Key, string.Empty);
                return true;
            }

            var sanitized = PlayerDisplayName.Sanitize(name, MaxLength);
            if (sanitized.Length == 0)
            {
                return false;
            }

            PlayerPrefs.SetString(Key, sanitized);
            PlayerPrefs.Save();
            Saved?.Invoke(Key, sanitized);
            return true;
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
