using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ガンゲーム (<see cref="GunGame"/>) の HUD の行 (表のキー <c>mode.gungame.*</c> の <see cref="LocalizedText"/>) と
    /// <c>[Mode] gungame</c> のログの文言。段は 1 から数えて書く。
    /// </summary>
    public static class GunGameText
    {
        public const string LogPrefix = ModeLog.Tag + " gungame";

        /// <summary>最後の段の人に出す、次の武器の HUD の行。</summary>
        public static LocalizedText LastTier => new LocalizedText("mode.gungame.lastTier");

        /// <summary>
        /// 今の段と武器の HUD の行。tier は 0 から数えた段で、最後の段を終えた人 (段の数) は最後の段として書く。
        /// weaponName は武器の名前 (<see cref="GunGameTier.Name"/>) で、<c>default</c> なら <c>-</c> を出す。
        /// </summary>
        /// <example><c>Tier(1, 3, new LocalizedText("weapon.launcher"))</c> は <c>段 2/3 Launcher</c></example>
        public static LocalizedText Tier(int tier, int tierCount, LocalizedText weaponName)
        {
            return new LocalizedText("mode.gungame.tier", DisplayedTier(tier, tierCount), tierCount, Name(weaponName));
        }

        /// <summary>次の武器の HUD の行。最後の段なら (nextWeaponName が <c>default</c>) <see cref="LastTier"/>。</summary>
        /// <example><c>Next(new LocalizedText("weapon.handgun"))</c> は <c>次: Handgun</c>、<c>Next(default)</c> は <c>次: なし (この武器で倒せば勝ち)</c></example>
        public static LocalizedText Next(LocalizedText nextWeaponName)
        {
            return nextWeaponName.IsEmpty ? LastTier : new LocalizedText("mode.gungame.next", nextWeaponName);
        }

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

        private static int DisplayedTier(int tier, int tierCount)
        {
            if (tier < 0)
            {
                return 1;
            }

            return tier >= tierCount ? tierCount : tier + 1;
        }

        private static object Name(LocalizedText weaponName)
        {
            return weaponName.IsEmpty ? (object)"-" : weaponName;
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
