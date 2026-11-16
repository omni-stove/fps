namespace Fps.Core
{
    /// <summary>
    /// Interact で拾う物の近くで出す案内の文 (表のキー <c>hud.pickup.*</c> の <see cref="LocalizedText"/>)。
    /// どの文も <c>{0}</c> は Interact のキー。武器とアイテムの文の <c>{1}</c> は名前 (<see cref="WeaponSpec.Name"/>・<see cref="ItemSpec.NameKey"/>) を
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

        /// <summary>outcome の物を拾う案内。拾えない outcome なら案内は出さない (false)。</summary>
        /// <param name="outcome"><see cref="PickupJudge.Judge"/> の結果。</param>
        /// <param name="keyLabel">Interact のキーの表示。null は空文字にする。</param>
        /// <param name="weaponName">武器かアイテムの名前。武器とアイテムの中身のときだけ使う。</param>
        /// <param name="text">出す文。出さないときは <c>default</c>。</param>
        public static bool TryDescribe(PickupOutcome outcome, string keyLabel, LocalizedText weaponName, out LocalizedText text)
        {
            text = default;
            if (!outcome.IsCollected)
            {
                return false;
            }

            var key = keyLabel ?? string.Empty;
            switch (outcome.Kind)
            {
                case PickupKind.Weapon:
                    var weaponKey = outcome.DroppedWeaponId != null ? WeaponSwapKey : outcome.AddsWeapon ? WeaponKey : WeaponAmmoKey;
                    text = new LocalizedText(weaponKey, key, weaponName);
                    return true;
                case PickupKind.ReserveAmmo:
                    text = new LocalizedText(ReserveAmmoKey, key);
                    return true;
                case PickupKind.Health:
                    text = new LocalizedText(HealthKey, key);
                    return true;
                case PickupKind.Armor:
                    text = new LocalizedText(ArmorKey, key);
                    return true;
                case PickupKind.Tag:
                    text = new LocalizedText(TagKey, key);
                    return true;
                case PickupKind.Item:
                    text = new LocalizedText(ItemKey, key, weaponName, outcome.Items.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return true;
                default:
                    return false;
            }
        }
    }
}
