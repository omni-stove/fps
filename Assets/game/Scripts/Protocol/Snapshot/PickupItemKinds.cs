using System;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 拾い物の中身の種類 (PickupKind) とスナップショットの物の種類 (ObjectiveItemKind) の対応。サーバーとクライアントで同じ表を使う
    public static class PickupItemKinds
    {
        // タグはモード (Kill Confirmed) が ObjectiveItemKind.Tag で送るので、ここでは扱わない
        public static bool TryGetItemKind(PickupKind kind, out ObjectiveItemKind itemKind)
        {
            switch (kind)
            {
                case PickupKind.Weapon:
                    itemKind = ObjectiveItemKind.WeaponPickup;
                    return true;
                case PickupKind.ReserveAmmo:
                    itemKind = ObjectiveItemKind.AmmoPickup;
                    return true;
                case PickupKind.Health:
                    itemKind = ObjectiveItemKind.HealthPickup;
                    return true;
                case PickupKind.Armor:
                    itemKind = ObjectiveItemKind.ArmorPickup;
                    return true;
                case PickupKind.Item:
                    itemKind = ObjectiveItemKind.ItemPickup;
                    return true;
                default:
                    itemKind = default;
                    return false;
            }
        }

        public static ObjectiveItemKind ItemKindOf(PickupKind kind)
        {
            if (!TryGetItemKind(kind, out var itemKind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "a " + kind + " pickup is not sent as a pickup item");
            }

            return itemKind;
        }

        public static bool TryGetPickupKind(ObjectiveItemKind itemKind, out PickupKind kind)
        {
            switch (itemKind)
            {
                case ObjectiveItemKind.WeaponPickup:
                    kind = PickupKind.Weapon;
                    return true;
                case ObjectiveItemKind.AmmoPickup:
                    kind = PickupKind.ReserveAmmo;
                    return true;
                case ObjectiveItemKind.HealthPickup:
                    kind = PickupKind.Health;
                    return true;
                case ObjectiveItemKind.ArmorPickup:
                    kind = PickupKind.Armor;
                    return true;
                case ObjectiveItemKind.ItemPickup:
                    kind = PickupKind.Item;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        public static bool IsPickup(ObjectiveItemKind itemKind)
        {
            return TryGetPickupKind(itemKind, out _);
        }

        // 拾い物か弾薬箱。どちらも拾う要求 (PickupRequestMessage) で使う
        public static bool IsCollectable(ObjectiveItemKind itemKind)
        {
            return itemKind == ObjectiveItemKind.AmmoCrate || IsPickup(itemKind);
        }
    }
}
