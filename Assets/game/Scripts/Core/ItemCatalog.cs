using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ItemCatalog
    {
        // 持ち物を送る電文がアイテムを表の番号 (byte) で指す
        public const int MaxItems = byte.MaxValue;

        public static readonly ItemCatalog Empty = new ItemCatalog(Array.Empty<ItemSpec>(), InventoryRule.Default);

        public ItemCatalog(IEnumerable<ItemSpec> items, InventoryRule rule, IEnumerable<ItemCount> starting = null)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            var list = new List<ItemSpec>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("items must not contain null", nameof(items));
                }

                if (IndexIn(list, item.Id) >= 0)
                {
                    throw new ArgumentException("item " + item.Id + " is listed twice", nameof(items));
                }

                if (item.IsWeapon && WeaponIndexIn(list, item.WeaponId) >= 0)
                {
                    throw new ArgumentException("weapon " + item.WeaponId + " is listed as more than one item", nameof(items));
                }

                list.Add(item);
            }

            foreach (var item in list)
            {
                if (item.AmmoItemId == null)
                {
                    continue;
                }

                var ammo = IndexIn(list, item.AmmoItemId);
                if (ammo < 0)
                {
                    throw new ArgumentException("ammo item " + item.AmmoItemId + " of weapon item " + item.Id + " is not one of the items", nameof(items));
                }

                if (list[ammo].IsWeapon)
                {
                    throw new ArgumentException("ammo item " + item.AmmoItemId + " of weapon item " + item.Id + " is a weapon", nameof(items));
                }
            }

            if (list.Count > MaxItems)
            {
                throw new ArgumentException("at most " + MaxItems + " items can be listed", nameof(items));
            }

            Items = list.AsReadOnly();
            var start = new List<ItemCount>();
            if (starting != null)
            {
                foreach (var entry in starting)
                {
                    if (entry.ItemId == null)
                    {
                        throw new ArgumentException("starting must not contain an empty entry", nameof(starting));
                    }

                    var at = IndexIn(list, entry.ItemId);
                    if (at < 0)
                    {
                        throw new ArgumentException("starting item " + entry.ItemId + " is not one of the items", nameof(starting));
                    }

                    // 武器の山は持ち武器から作るので、持ち物だけに武器を入れると撃てない武器が入ってしまう
                    if (list[at].IsWeapon)
                    {
                        throw new ArgumentException("starting item " + entry.ItemId + " is a weapon; weapons come from the loadout", nameof(starting));
                    }

                    start.Add(entry);
                }
            }

            Starting = start.AsReadOnly();
        }

        public IReadOnlyList<ItemSpec> Items { get; }

        public InventoryRule Rule { get; }

        public IReadOnlyList<ItemCount> Starting { get; }

        public bool TryFind(string id, out ItemSpec item)
        {
            var at = IndexOf(id);
            item = at >= 0 ? Items[at] : null;
            return item != null;
        }

        public int IndexOf(string id)
        {
            return string.IsNullOrEmpty(id) ? -1 : IndexIn(Items, id);
        }

        public bool TryFindWeapon(string weaponId, out ItemSpec item)
        {
            var at = string.IsNullOrEmpty(weaponId) ? -1 : WeaponIndexIn(Items, weaponId);
            item = at >= 0 ? Items[at] : null;
            return item != null;
        }

        public bool TryFindAmmo(string weaponId, out ItemSpec ammo)
        {
            ammo = null;
            return TryFindWeapon(weaponId, out var weapon) && weapon.AmmoItemId != null && TryFind(weapon.AmmoItemId, out ammo);
        }

        public bool TryFindAttachment(string itemId, out ItemSpec attachment)
        {
            attachment = TryFind(itemId, out var item) && item.IsAttachment ? item : null;
            return attachment != null;
        }

        public IReadOnlyList<string> SlotsOf(string weaponId)
        {
            return TryFindWeapon(weaponId, out var weapon) ? weapon.AttachmentSlots : Array.Empty<string>();
        }

        public bool CanAttach(string weaponId, string attachmentItemId)
        {
            return TryFindAttachment(attachmentItemId, out var attachment)
                && TryFindWeapon(weaponId, out var weapon)
                && weapon.HasSlot(attachment.Attachment.Slot)
                && attachment.Attachment.Fits(weaponId);
        }

        // サーバーから届いた表は一部のアイテムしか持たないので、作るときではなく読むときに確かめる
        public void CheckAttachments()
        {
            foreach (var item in Items)
            {
                if (!item.IsAttachment)
                {
                    continue;
                }

                foreach (var weaponId in item.Attachment.WeaponIds)
                {
                    if (!TryFindWeapon(weaponId, out var weapon))
                    {
                        throw new ArgumentException("attachment item " + item.Id + " fits weapon " + weaponId + ", which is not one of the items", "items");
                    }

                    if (!weapon.HasSlot(item.Attachment.Slot))
                    {
                        throw new ArgumentException(
                            "attachment item " + item.Id + " goes on slot '" + item.Attachment.Slot + "', which weapon item " + weapon.Id + " does not have", "items");
                    }
                }
            }
        }

        private static int WeaponIndexIn(IReadOnlyList<ItemSpec> items, string weaponId)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].WeaponId, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexIn(IReadOnlyList<ItemSpec> items, string id)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
