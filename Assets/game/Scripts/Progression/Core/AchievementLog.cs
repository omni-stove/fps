using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class AchievementLog
    {
        public const string LogTag = "[Achievement]";

        public static string Configured(AchievementSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} configured achievements={1} melee={2}",
                LogTag, spec.Achievements.Count, string.Join(",", spec.MeleeWeaponIds));
        }

        public static string Unlocked(AchievementDefinition achievement)
        {
            if (achievement == null)
            {
                throw new ArgumentNullException(nameof(achievement));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} unlocked id={1} stat={2} target={3}",
                LogTag, achievement.Id, achievement.Stat, achievement.Target);
        }

        public static string PlayTime(double seconds, int total)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} play time +{1:0.#}s total={2}s", LogTag, seconds, total);
        }
    }
}
