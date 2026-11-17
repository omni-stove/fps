using System.Globalization;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// Interact で拾う物の近くで出す案内 (<see cref="PickupHint"/>) の文 (表のキー <c>hud.pickup.*</c> の <see cref="LocalizedText"/>)。
    /// どの文も <c>{0}</c> は Interact のキー。武器とアイテムの文の <c>{1}</c> は名前 (武器は <see cref="WeaponSpec.NameOf"/>・アイテムは表示名のキー) を
    /// 入れ子の文として入れ、アイテムの文の <c>{2}</c> は持ち物に入る数。
    /// </summary>
    public static class PickupPrompt
    {
        /// <summary>持っていない武器を拾う。{0} はキー、{1} は武器の名前。</summary>
        public const string WeaponKey = "hud.pickup.weapon";

        /// <summary>持てる数の上限に届いているので、今の武器を落として持ち替える。{0} はキー、{1} は拾う武器の名前。</summary>
        public const string WeaponSwapKey = "hud.pickup.weaponSwap";

        /// <summary>持っている武器を拾って予備弾にする。{0} はキー、{1} は武器の名前。</summary>
        public const string WeaponAmmoKey = "hud.pickup.weaponAmmo";

        public const string ReserveAmmoKey = "hud.pickup.reserveAmmo";
        public const string HealthKey = "hud.pickup.health";
        public const string ArmorKey = "hud.pickup.armor";
        public const string TagKey = "hud.pickup.tag";

        public const string ItemKey = "hud.pickup.item";

        /// <summary>hint の物を拾う案内の文。</summary>
        /// <param name="keyLabel">Interact のキーの表示。null は空文字にする。</param>
        /// <param name="text">出す文。<see cref="PickupHint.Action"/> が知らない値なら <c>default</c>。</param>
        public static bool TryDescribe(PickupHint hint, string keyLabel, out LocalizedText text)
        {
            var key = keyLabel ?? string.Empty;
            switch (hint.Action)
            {
                case PickupHintAction.TakeWeapon:
                    text = new LocalizedText(WeaponKey, key, WeaponName(hint));
                    return true;
                case PickupHintAction.SwapWeapon:
                    text = new LocalizedText(WeaponSwapKey, key, WeaponName(hint));
                    return true;
                case PickupHintAction.WeaponAmmo:
                    text = new LocalizedText(WeaponAmmoKey, key, WeaponName(hint));
                    return true;
                case PickupHintAction.ReserveAmmo:
                    text = new LocalizedText(ReserveAmmoKey, key);
                    return true;
                case PickupHintAction.Health:
                    text = new LocalizedText(HealthKey, key);
                    return true;
                case PickupHintAction.Armor:
                    text = new LocalizedText(ArmorKey, key);
                    return true;
                case PickupHintAction.Tag:
                    text = new LocalizedText(TagKey, key);
                    return true;
                case PickupHintAction.Item:
                    var itemName = hint.NameKey.Length > 0 ? new LocalizedText(hint.NameKey) : default;
                    text = new LocalizedText(ItemKey, key, itemName, hint.Count.ToString(CultureInfo.InvariantCulture));
                    return true;
                default:
                    text = default;
                    return false;
            }
        }

        private static LocalizedText WeaponName(PickupHint hint)
        {
            return WeaponSpec.NameOf(hint.ContentId, hint.NameKey);
        }
    }
}
