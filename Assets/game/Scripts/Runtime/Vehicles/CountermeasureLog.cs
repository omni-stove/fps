using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class CountermeasureLog
    {
        public const string Tag = VehicleLog.Tag;

        public const string ReasonEmpty = "empty";

        public const string ReasonCoolingDown = "cooling_down";

        public static string Deployed(string vehicle, int flareId, int remaining, int count)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} '{1}' flare deployed id={2} remaining={3}/{4}", Tag, vehicle, flareId, remaining, count);
        }

        public static string Refused(string vehicle, CountermeasureVerdict verdict, int remaining, int count, float cooldownLeft)
        {
            var reason = verdict == CountermeasureVerdict.Empty ? ReasonEmpty : ReasonCoolingDown;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' flare refused reason={2} remaining={3}/{4} wait={5:F2}",
                Tag,
                vehicle,
                reason,
                remaining,
                count,
                cooldownLeft);
        }

        public static string Refilled(string vehicle, int count)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' flare refilled remaining={2}/{2}", Tag, vehicle, count);
        }

        public static string InvalidSettings(string vehicle, string message)
        {
            return $"{Tag} '{vehicle}' のフレアの設定を使えないので既定の値を使います: {message}";
        }

        public static string Lured(string weaponId, int flareId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} projectile weapon={1} lured flare={2}", ProjectileBody.LogTag, weaponId, flareId);
        }
    }
}
