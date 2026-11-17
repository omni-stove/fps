using System.Globalization;

namespace Shooter.Core
{
    public static class RumbleLog
    {
        public const string Tag = "[Rumble]";

        public static string Pulse(string source, RumblePulse pulse, string detail)
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0} pulse source={1} low={2:0.000} high={3:0.000} seconds={4:0.00}",
                Tag,
                string.IsNullOrEmpty(source) ? "unknown" : source,
                pulse.Low,
                pulse.High,
                pulse.Seconds);
            return string.IsNullOrEmpty(detail) ? line : line + " " + detail;
        }

        public static string Stopped(string reason)
        {
            return Tag + " stopped reason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason);
        }

        public static string DeviceChanged(string from, string to)
        {
            return Tag + " device changed from=" + Token(from) + " to=" + Token(to);
        }

        public static string Distance(float distance)
        {
            return "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string Amount(int amount)
        {
            return "amount=" + amount.ToString(CultureInfo.InvariantCulture);
        }

        public static string Weapon(string weaponId)
        {
            return "weapon=" + Token(weaponId);
        }

        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            return text.Trim().Replace(' ', '_');
        }
    }
}
