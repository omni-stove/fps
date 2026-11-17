using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class PickupPrompt
    {
        public const string WeaponKey = "hud.pickup.weapon";

        public const string WeaponSwapKey = "hud.pickup.weaponSwap";

        public const string WeaponAmmoKey = "hud.pickup.weaponAmmo";

        public const string ReserveAmmoKey = "hud.pickup.reserveAmmo";
        public const string HealthKey = "hud.pickup.health";
        public const string ArmorKey = "hud.pickup.armor";
        public const string TagKey = "hud.pickup.tag";

        public const string ItemKey = "hud.pickup.item";

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
            return DisplayNames.Of(hint.NameKey, hint.ContentId);
        }
    }
}
