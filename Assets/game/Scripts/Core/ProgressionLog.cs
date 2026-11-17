using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class ProgressionLog
    {
        public const string LogTag = "[Progression]";

        public static string Configured(ProgressionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return LogTag + " configured " + spec;
        }

        public static string Loaded(PlayerProfile profile, int level)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} profile {1} level={2}", LogTag, profile, level);
        }

        public static string Killed(bool headshot, ProgressionAward award)
        {
            if (award == null)
            {
                throw new ArgumentNullException(nameof(award));
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0} kill head={1} {2}", LogTag, headshot ? "true" : "false", Gained(award));
        }

        public static string Assisted(ProgressionAward award)
        {
            if (award == null)
            {
                throw new ArgumentNullException(nameof(award));
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} assist {1}", LogTag, Gained(award));
        }

        public static string MatchRecorded(MatchResult result, ProgressionAward award)
        {
            if (award == null)
            {
                throw new ArgumentNullException(nameof(award));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} match outcome={1} kills={2} deaths={3} {4}",
                LogTag, result.Outcome, result.Kills, result.Deaths, Gained(award));
        }

        public static string ProfileRejected(string reason)
        {
            return LogTag + " profile rejected: " + reason;
        }

        public static string ProfileKept(string slot)
        {
            return LogTag + " kept the rejected profile in slot=" + slot;
        }

        public static string LeveledUp(ProgressionAward award)
        {
            if (award == null)
            {
                throw new ArgumentNullException(nameof(award));
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0} level up {1}->{2} total={3}", LogTag, award.PreviousLevel, award.Level, award.Profile.Xp);
        }

        public static string Saved(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return LogTag + " saved " + profile;
        }

        public static string SaveFailed(string reason)
        {
            return LogTag + " could not save the profile: " + reason;
        }

        private static string Gained(ProgressionAward award)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "xp=+{0} total={1} level={2} levelUp={3}",
                award.Xp, award.Profile.Xp, award.Level, award.LeveledUp ? "true" : "false");
        }
    }
}
