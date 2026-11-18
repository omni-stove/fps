using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class StatsFormat
    {
        public static string Title => GameText.Get("stats.title");

        public static string Back => GameText.Get("stats.back");

        public static string RecordTitle => GameText.Get("stats.recordTitle");

        public static string HiddenAchievement => GameText.Get("stats.achievement.hidden");

        public static string NoAchievements => GameText.Get("stats.achievement.none");

        public static string Matches(int matches)
        {
            return GameText.Format("stats.matches", matches);
        }

        public static string Wins(int wins)
        {
            return GameText.Format("stats.wins", wins);
        }

        public static string WinRate(int wins, int matches)
        {
            if (wins < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wins), wins, "wins must not be negative");
            }

            if (matches < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matches), matches, "matches must not be negative");
            }

            // 1 試合も遊んでいないときに NaN を出さないよう、試合数を 1 として割る (勝ちも 0 なので 0% になる)
            var percent = Math.Round(100d * wins / Math.Max(1, matches), MidpointRounding.AwayFromZero);
            return GameText.Format("stats.winRate", percent.ToString("0", CultureInfo.InvariantCulture));
        }

        public static string Kills(int kills)
        {
            return GameText.Format("stats.kills", kills);
        }

        public static string Assists(int assists)
        {
            return GameText.Format("stats.assists", assists);
        }

        public static string Headshots(int headshots)
        {
            return GameText.Format("stats.headshots", headshots);
        }

        public static string MeleeKills(int meleeKills)
        {
            return GameText.Format("stats.meleeKills", meleeKills);
        }

        // 遊んだ時間は長くなるので、秒までは出さずに時間と分にする (1 分に満たない分は切り捨てる)
        public static string PlayTime(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return GameText.Format("stats.playTime", seconds / 3600, seconds % 3600 / 60);
        }

        public static string AchievementsTitle(int unlocked, int total)
        {
            return GameText.Format("stats.achievementsTitle", unlocked, total);
        }

        public static string Achievement(AchievementDefinition achievement, AchievementProgress progress)
        {
            if (achievement == null)
            {
                throw new ArgumentNullException(nameof(achievement));
            }

            // 隠し実績は解けるまで名前も条件も見せない
            if (achievement.Hidden && !progress.Unlocked)
            {
                return HiddenAchievement;
            }

            var name = GameText.Resolve(DisplayNames.Of(achievement.DisplayNameKey, achievement.Id));
            return progress.Unlocked
                ? GameText.Format("stats.achievement.unlocked", name)
                : GameText.Format("stats.achievement.progress", name, Math.Min(progress.Current, progress.Target), progress.Target);
        }

        public static string AchievementDescription(AchievementDefinition achievement, AchievementProgress progress)
        {
            if (achievement == null)
            {
                throw new ArgumentNullException(nameof(achievement));
            }

            if ((achievement.Hidden && !progress.Unlocked) || achievement.DescriptionKey.Length == 0)
            {
                return string.Empty;
            }

            return GameText.Get(achievement.DescriptionKey);
        }
    }
}
