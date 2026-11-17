using System.Globalization;

namespace Shooter.Core
{
    public static class WaterLog
    {
        public const string LogTag = "[Water]";

        public static string Invalid(string water, string reason)
        {
            return $"{LogTag} '{NameOf(water)}' is ignored: {reason}";
        }

        public static string Drown(string target, int damage, int health)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} drown target={1} dmg={2} hp={3}", LogTag, NameOf(target), damage, health);
        }

        private static string NameOf(string name)
        {
            return string.IsNullOrEmpty(name) ? "-" : name;
        }
    }
}
