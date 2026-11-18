using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ReplayText
    {
        public const string TitleKey = "replay.title";
        public const string EmptyKey = "replay.empty";
        public const string UnavailableKey = "replay.unavailable";
        public const string EntryKey = "replay.entry";
        public const string DateKey = "replay.date";
        public const string DurationKey = "replay.duration";
        public const string PlayKey = "replay.play";
        public const string DeleteKey = "replay.delete";
        public const string ConfirmDeleteKey = "replay.confirmDelete";
        public const string BackKey = "replay.back";
        public const string PlayFailedKey = "replay.playFailed";
        public const string DeleteFailedKey = "replay.deleteFailed";

        public static string Title => GameText.Get(TitleKey);

        public static string Empty => GameText.Get(EmptyKey);

        public static string Unavailable => GameText.Get(UnavailableKey);

        public static string Play => GameText.Get(PlayKey);

        public static string Delete => GameText.Get(DeleteKey);

        public static string ConfirmDelete => GameText.Get(ConfirmDeleteKey);

        public static string Back => GameText.Get(BackKey);

        public static string PlayFailed => GameText.Get(PlayFailedKey);

        public static string DeleteFailed => GameText.Get(DeleteFailedKey);

        // 文言の表は {0} の置き換えだけで書式指定を読まないので、桁をそろえた文字にしてから渡す。
        // 一覧は端末の時刻で見せるので、残した UTC を地域の時刻に直してから並べる。
        public static string Date(DateTime recordedAtUtc)
        {
            var local = recordedAtUtc.ToLocalTime();
            return GameText.Format(
                DateKey,
                local.Year.ToString(CultureInfo.InvariantCulture),
                TwoDigits(local.Month),
                TwoDigits(local.Day),
                TwoDigits(local.Hour),
                TwoDigits(local.Minute));
        }

        public static string Duration(float seconds)
        {
            var total = float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f ? 0 : (int)Math.Min(int.MaxValue, Math.Round(seconds));
            return GameText.Format(DurationKey, (total / 60).ToString(CultureInfo.InvariantCulture), TwoDigits(total % 60));
        }

        private static string TwoDigits(int value)
        {
            return value.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string Entry(ReplayEntry entry)
        {
            return GameText.Format(EntryKey, MapVoteView.MapName(entry.MapId), Date(entry.RecordedAtUtc), Duration(entry.DurationSeconds));
        }
    }
}
