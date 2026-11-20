using System;
using System.Globalization;

namespace Shooter.Runtime
{
    public static class ProgressionFormat
    {
        public static string Title => GameText.Get("progression.title");

        public static string Back => GameText.Get("progression.back");

        public static string StatsTitle => GameText.Get("progression.statsTitle");

        public static string NextUnlocksTitle => GameText.Get("progression.nextUnlocksTitle");

        public static string AllUnlocked => GameText.Get("progression.allUnlocked");

        public static string MaxLevel => GameText.Get("progression.maxLevel");

        public static string Locked => GameText.Get("progression.locked");

        public static string NameTitle => GameText.Get("progression.nameTitle");

        public static string NameHint(int maxLength)
        {
            return GameText.Format("progression.nameHint", maxLength);
        }

        public static string NameRejected(int maxLength)
        {
            return GameText.Format("progression.nameRejected", maxLength);
        }

        public static string Level(int level)
        {
            return GameText.Format("progression.level", level);
        }

        public static string TotalXp(int xp)
        {
            return GameText.Format("progression.totalXp", xp);
        }

        public static string XpToNext(int xp, int nextLevel)
        {
            return GameText.Format("progression.xpToNext", xp, nextLevel);
        }

        public static string Kills(int kills)
        {
            return GameText.Format("progression.kills", kills);
        }

        public static string Assists(int assists)
        {
            return GameText.Format("assist.profile.assists", assists);
        }

        public static string Deaths(int deaths)
        {
            return GameText.Format("progression.deaths", deaths);
        }

        public static string KillDeathRatio(int kills, int deaths)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            if (deaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deaths), deaths, "deaths must not be negative");
            }

            // 1 度も倒れていないときに無限大や NaN を出さないよう、倒れた数を 1 として割る (倒した数がそのまま比になる)
            var ratio = (double)kills / Math.Max(1, deaths);
            return GameText.Format("progression.killDeathRatio", ratio.ToString("0.00", CultureInfo.InvariantCulture));
        }

        public static string Matches(int matches)
        {
            return GameText.Format("progression.matches", matches);
        }

        public static string Wins(int wins)
        {
            return GameText.Format("progression.wins", wins);
        }

        public static string Unlock(int level, string name)
        {
            return GameText.Format("progression.unlock", level, name ?? string.Empty);
        }

        public static string LockedUntil(int level)
        {
            return GameText.Format("progression.lockedUntil", level);
        }

        public static string AttachmentLocked(int level)
        {
            return GameText.Format("progression.attachmentLocked", level);
        }
    }
}
