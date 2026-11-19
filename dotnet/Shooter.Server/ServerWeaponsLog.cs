using System.Globalization;

namespace Shooter.Server
{
    internal static class ServerWeaponsLog
    {
        public const string Tag = "[Net]";

        // rejected が null なら持たせた
        public static string Give(int playerId, string weaponId, string rejected)
        {
            return Tag + " give weapon player=" + Text(playerId) + " weapon=" + weaponId + (rejected == null ? string.Empty : " rejected=" + rejected);
        }

        public static string Clear(int playerId, int weapons)
        {
            return Tag + " clear weapons player=" + Text(playerId) + " weapons=" + Text(weapons);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
