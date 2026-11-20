using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class SkillLog
    {
        public const string LogTag = "[Skill]";

        public static string Configured(SkillSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return LogTag + " configured " + spec;
        }

        public static string Loaded(SkillSave save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            return LogTag + " loaded " + save;
        }

        public static string Rated(MatchOutcome outcome, int previous, int rating, int opponents)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} rating {1}->{2} outcome={3} opponents={4}",
                LogTag, previous, rating, outcome, opponents);
        }

        public static string Rejected(string reason)
        {
            return LogTag + " skill save rejected: " + reason;
        }

        public static string Kept(string slot)
        {
            return LogTag + " kept the rejected skill save in slot=" + slot;
        }

        public static string Saved(SkillSave save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            return LogTag + " saved " + save;
        }

        public static string SaveFailed(string reason)
        {
            return LogTag + " could not save the rating: " + reason;
        }
    }
}
