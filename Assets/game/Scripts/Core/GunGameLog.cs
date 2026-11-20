using System.Globalization;

namespace Shooter.Core
{
    public static class GunGameLog
    {
        public const string LogPrefix = ModeLog.Tag + " gungame";

        public static string Promote(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("promote", playerId, tier, tierCount, weaponName);
        }

        public static string Demote(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("demote", playerId, tier, tierCount, weaponName);
        }

        public static string Finish(int playerId, string weaponName)
        {
            return LogPrefix + " finish player=" + Number(playerId) + " weapon=" + Name(weaponName);
        }

        public static string Equip(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("equip", playerId, tier, tierCount, weaponName);
        }

        private static string Change(string action, int playerId, int tier, int tierCount, string weaponName)
        {
            return LogPrefix + " " + action + " player=" + Number(playerId)
                + " tier=" + Number(GunGameDisplayedTier.Of(tier, tierCount)) + "/" + Number(tierCount)
                + " weapon=" + Name(weaponName);
        }

        private static string Name(string weaponName)
        {
            return string.IsNullOrWhiteSpace(weaponName) ? "-" : weaponName;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
