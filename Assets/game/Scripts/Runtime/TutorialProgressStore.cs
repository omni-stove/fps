using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 射撃場のチュートリアルを終えたことを <see cref="PlayerPrefs"/> の 1 つのキーに整数 (終えたら 1) で読み書きする。
    /// </summary>
    public sealed class TutorialProgressStore
    {
        /// <summary>ゲームが使う PlayerPrefs のキー。</summary>
        public const string DefaultKey = "Fps.TutorialCompleted";

        /// <param name="key">PlayerPrefs のキー。テストでは本物の記録を壊さないよう別のキーを渡す。</param>
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

        /// <summary>読み書きする PlayerPrefs のキー。</summary>
        public string Key { get; }

        /// <summary>
        /// チュートリアルを終えたことがあるか。キーが無いか 1 以外 (同じキーに文字列などが入っている) なら false。
        /// </summary>
        public bool IsCompleted => PlayerPrefs.GetInt(Key, 0) == 1;

        /// <summary>終えたことを記録し、すぐディスクに書き出す。</summary>
        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>記録を消す。次に射撃場へ入るとチュートリアルを出す。</summary>
        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
