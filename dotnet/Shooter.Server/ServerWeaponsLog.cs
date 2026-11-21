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

        public static string Replaced(int playerId, string weaponId, string replacedWeaponId)
        {
            return Give(playerId, weaponId, null) + " replaced=" + replacedWeaponId;
        }

        public static string Clear(int playerId, int weapons)
        {
            return Tag + " clear weapons player=" + Text(playerId) + " weapons=" + Text(weapons);
        }

        public static string Drop(int playerId, string weaponId, int weaponsLeft)
        {
            return Tag + " drop weapon player=" + Text(playerId) + " weapon=" + weaponId + " left=" + Text(weaponsLeft);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
