using System;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// <see cref="PlayerProfile"/> を <see cref="ISaveStorage"/> の <see cref="Slot"/> の枠に JSON (<see cref="PlayerProfile.ToJson"/>) で読み書きする。
    /// 読めない内容 (壊れている・知らない版) なら <c>[Progression]</c> の警告を出し、内容を <see cref="RejectedSlot"/> に移して新しいプロフィールから始める。
    /// </summary>
    public sealed class ProfileStore
    {
        public const string Slot = "profile";

        // 次の保存で上書きして消してしまわないよう、読めなかった内容を別の枠に残す (新しい版のゲームで書いたものを古い版で開いたときなど)。
        public const string RejectedSlot = "profile-rejected";

        /// <exception cref="ArgumentNullException">storage が null。</exception>
        public ProfileStore(ISaveStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public ISaveStorage Storage { get; }

        /// <summary>保存したプロフィールを読む。まだ無ければ <see cref="PlayerProfile.Empty"/>。読めなければ警告を出して <see cref="PlayerProfile.Empty"/>。</summary>
        public PlayerProfile Load()
        {
            if (!Storage.TryRead(Slot, out var text) || text == null)
            {
                return PlayerProfile.Empty;
            }

            try
            {
                return PlayerProfile.FromJson(text);
            }
            catch (FormatException e)
            {
                Debug.LogWarning(ProgressionLog.ProfileRejected(e.Message));
                KeepRejected(text);
                return PlayerProfile.Empty;
            }
        }

        /// <summary>profile を <see cref="Slot"/> に書く。</summary>
        /// <exception cref="ArgumentNullException">profile が null。</exception>
        /// <exception cref="IOException">ストレージに書けない。前の内容は残る (<see cref="ISaveStorage.Write"/> の決まり)。</exception>
        /// <exception cref="UnauthorizedAccessException">ストレージに書く権限が無い。</exception>
        public void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Storage.Write(Slot, profile.ToJson());
        }

        private void KeepRejected(string text)
        {
            try
            {
                Storage.Write(RejectedSlot, text);
                Debug.LogWarning(ProgressionLog.ProfileKept(RejectedSlot));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 退避は失っても遊べるので、読み込みは止めずに新しいプロフィールで続ける。
                Debug.LogError(ProgressionLog.SaveFailed(e.Message));
            }
        }
    }
}
