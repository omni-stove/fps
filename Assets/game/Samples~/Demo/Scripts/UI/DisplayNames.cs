using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // データは表示名をキーの文字列でしか持たない (キットは言語を扱わない) ので、キーを文にするのはここに寄せる。
    // キーが空なら Id をそのまま出し、足したばかりの武器などでも名前の欄が空にならないようにする。
    public static class DisplayNames
    {
        public static LocalizedText Of(string nameKey, string id)
        {
            var key = nameKey == null ? string.Empty : nameKey.Trim();
            return key.Length > 0 ? new LocalizedText(key) : LocalizedText.Literal(id ?? string.Empty);
        }

        public static LocalizedText Of(WeaponSpec weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return Of(weapon.NameKey, weapon.Id);
        }

        public static LocalizedText Of(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return Of(weapon.NameKey, weapon.Id);
        }

        public static LocalizedText Of(GrenadeSpec grenade)
        {
            if (grenade == null)
            {
                throw new ArgumentNullException(nameof(grenade));
            }

            return Of(grenade.NameKey, grenade.Id);
        }

        public static LocalizedText Of(AmmoTypeSpec ammo)
        {
            if (ammo == null)
            {
                throw new ArgumentNullException(nameof(ammo));
            }

            return Of(ammo.NameKey, ammo.Id);
        }

        public static LocalizedText Of(GunGameTier tier)
        {
            return Of(tier.NameKey, tier.WeaponId);
        }

        public static LocalizedText Of(AbilityDefinition ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            return Of(ability.DisplayNameKey, ability.Id);
        }

        public static LocalizedText Of(StreakSlot slot)
        {
            return Of(slot.DisplayNameKey, slot.RewardId);
        }

        public static LocalizedText Of(BuyItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return Of(item.NameKey, item.Id);
        }

        // ミッションは Id を人に見せる名前にしていないので、キーが空なら Id ではなく空の文にする。
        public static LocalizedText Mission(string displayNameKey)
        {
            return string.IsNullOrWhiteSpace(displayNameKey) ? default : new LocalizedText(displayNameKey.Trim());
        }

        // 数を表の文に直に書くと、定義の数を変えたときに説明だけ古いまま残るので、必要な数を差し込む。
        public static LocalizedText Description(Objective objective)
        {
            if (objective == null)
            {
                throw new ArgumentNullException(nameof(objective));
            }

            return objective.DescriptionKey.Length == 0 ? default : new LocalizedText(objective.DescriptionKey, objective.RequiredCount);
        }
    }
}
