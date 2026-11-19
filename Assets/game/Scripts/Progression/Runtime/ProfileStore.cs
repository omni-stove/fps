using System;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ProfileStore
    {
        public const string Slot = "profile";

        // 次の保存で上書きして消してしまわないよう、読めなかった内容を別の枠に残す (新しい版のゲームで書いたものを古い版で開いたときなど)。
        public const string RejectedSlot = "profile-rejected";

        public ProfileStore(ISaveStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public ISaveStorage Storage { get; }

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
