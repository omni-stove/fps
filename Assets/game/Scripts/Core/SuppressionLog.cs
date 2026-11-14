using System;
using System.Globalization;

namespace Fps.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class SuppressionLog
    {
        public const string LogTag = "[Suppression]";

        public static string Configured(string id, Suppression rule)
        {
            return $"{LogTag} id={id} rule={rule}";
        }

        public static string NearMissed(string id, float distance, SuppressionMeter meter)
        {
            if (meter == null)
            {
                throw new ArgumentNullException(nameof(meter));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} near-miss distance={2:F2} level={3:F2} suppressed={4} count={5}",
                LogTag, id, distance, meter.Level, meter.IsSuppressed ? "true" : "false", meter.NearMisses);
        }

        public static string SuppressedChanged(string id, bool suppressed, float level)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} id={1} suppressed={2} level={3:F2}", LogTag, id, suppressed ? "true" : "false", level);
        }

        public static string Cleared(string id)
        {
            return $"{LogTag} id={id} cleared";
        }

        public static string HoldCover(string id, bool holding)
        {
            return $"{LogTag} id={id} hold-cover={(holding ? "true" : "false")}";
        }
    }
}
