using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ボイスチャットの設定 (<see cref="VoiceSettings"/>) を、<see cref="ControlSettingsStore"/> と同じ <see cref="PlayerPrefs"/> の
    /// 別のキーに JSON の文字列 (<see cref="VoiceSettings.ToJson"/>) で読み書きする。
    /// 読めなかった所は初期値 (範囲の外の音量は範囲に収めた値) に戻し、理由を <c>[Voice]</c> 付きの警告で出す。
    /// </summary>
    public sealed class VoiceSettingsStore
    {
        /// <summary>ゲームが使う PlayerPrefs のキー。</summary>
        public const string DefaultKey = "Shooter.VoiceSettings";

        /// <param name="key">PlayerPrefs のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
        public VoiceSettingsStore(string key = DefaultKey)
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

        /// <summary>どれかのストアが保存した。引数は保存したキーと設定。試合中のボイスが、設定の画面で変えた値をすぐ効かせるのに使う。</summary>
        public static event Action<string, VoiceSettings> Saved;

        /// <summary>読み書きする PlayerPrefs のキー。</summary>
        public string Key { get; }

        /// <summary>まだ保存していないときに <see cref="Load"/> が返す設定 (<see cref="VoiceSettings.Default"/>)。</summary>
        public VoiceSettings Defaults => VoiceSettings.Default;

        /// <summary>
        /// 保存した設定を読む。まだ保存していなければ警告を出さずに <see cref="Defaults"/> を返す。
        /// 壊れた値はその所だけ初期値にし、理由ごとに警告を出す。
        /// </summary>
        public VoiceSettings Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return Defaults;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として初期値に戻る
            var text = PlayerPrefs.GetString(Key, string.Empty);
            var settings = VoiceSettings.FromJson(text, out var problems);
            foreach (var problem in problems)
            {
                Debug.LogWarning(VoiceSettingsLog.Problem(Key, problem));
            }

            return settings;
        }

        /// <summary>settings を保存してすぐディスクに書き出し、<see cref="Saved"/> で知らせる。</summary>
        public void Save(VoiceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetString(Key, settings.ToJson());
            PlayerPrefs.Save();
            Saved?.Invoke(Key, settings);
        }

        /// <summary>保存した設定を消す。次の <see cref="Load"/> は初期値を返す。</summary>
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
