using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ガンゲーム (<see cref="GunGame"/>) の <c>[Mode] gungame</c> のログの文言。段は 1 から数えて書く。
    /// </summary>
    public static class GunGameText
    {
        public const string LogPrefix = ModeLog.Tag + " gungame";

        /// <summary>weaponName はログに書く武器の名前 (<see cref="GunGameTier.LogName"/>)。</summary>
        /// <example><c>[Mode] gungame promote player=0 tier=2/3 weapon=Launcher</c></example>
        public static string Promote(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("promote", playerId, tier, tierCount, weaponName);
        }

        /// <summary>近接の武器で倒されて 1 段戻った。</summary>
        /// <example><c>[Mode] gungame demote player=2 tier=1/3 weapon=Rifle</c></example>
        public static string Demote(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("demote", playerId, tier, tierCount, weaponName);
        }

        /// <summary>最後の段の武器で倒し、勝ちを決めた。</summary>
        /// <example><c>[Mode] gungame finish player=0 weapon=Handgun</c></example>
        public static string Finish(int playerId, string weaponName)
        {
            return LogPrefix + " finish player=" + Number(playerId) + " weapon=" + Name(weaponName);
        }

        /// <summary>始めたとき、またはリスポーンで持たせた武器。</summary>
        /// <example><c>[Mode] gungame equip player=1 tier=1/3 weapon=Rifle</c></example>
        public static string Equip(int playerId, int tier, int tierCount, string weaponName)
        {
            return Change("equip", playerId, tier, tierCount, weaponName);
        }

        private static string Change(string action, int playerId, int tier, int tierCount, string weaponName)
        {
            return LogPrefix + " " + action + " player=" + Number(playerId)
                + " tier=" + Number(DisplayedTier(tier, tierCount)) + "/" + Number(tierCount)
                + " weapon=" + Name(weaponName);
        }

        public static int DisplayedTier(int tier, int tierCount)
        {
            if (tier < 0)
            {
                return 1;
            }

            return tier >= tierCount ? tierCount : tier + 1;
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
