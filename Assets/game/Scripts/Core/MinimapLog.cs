using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class MinimapLog
    {
        public const string LogTag = "[Minimap]";

        public static string Configured(MinimapSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return $"{LogTag} configured {spec}";
        }

        public static string Contacts(int allies, int enemies, int pings, int objectives)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} contacts allies={1} enemies={2} pings={3} objectives={4}",
                LogTag, allies, enemies, pings, objectives);
        }

        public static string ValueReplaced(string source, string item, float value, float fallback)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} item '{2}' is {3}, which is not positive and finite; using the default {4} instead",
                LogTag, source, item, value, fallback);
        }
    }
}
