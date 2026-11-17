using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class InventoryAttachments
    {
        public static AttachmentRejection Attach(
            Inventory inventory,
            int handle,
            string weaponId,
            IReadOnlyList<string> current,
            Func<IReadOnlyList<string>, bool> setAttachments,
            out string itemId,
            out string replaced,
            Func<string, bool> isAvailable = null)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (setAttachments == null)
            {
                throw new ArgumentNullException(nameof(setAttachments));
            }

            itemId = null;
            replaced = null;
            if (!inventory.TryGetStack(handle, out var stack))
            {
                return AttachmentRejection.NoStack;
            }

            var catalog = inventory.Catalog;
            if (!catalog.TryFindAttachment(stack.ItemId, out var item))
            {
                return AttachmentRejection.NotAnAttachment;
            }

            itemId = item.Id;
            if (isAvailable != null && !isAvailable(item.Id))
            {
                return AttachmentRejection.Locked;
            }

            if (string.IsNullOrEmpty(weaponId))
            {
                return AttachmentRejection.NoWeapon;
            }

            var next = WeaponAttachments.Attach(catalog, weaponId, current, item.Id, out replaced);
            if (next == null)
            {
                return AttachmentRejection.DoesNotFit;
            }

            // 付け替えでは持ち物と武器を合わせた重さは変わらない。外した物の方が重いときだけ、その差を持ち物が軽くなる分として見る
            var replacedWeight = replaced != null && catalog.TryFind(replaced, out var off) ? off.Weight : 0f;
            if (replaced != null && !inventory.HasRoomAfterTaking(handle, replaced, Math.Max(0f, replacedWeight - item.Weight)))
            {
                return AttachmentRejection.NoRoom;
            }

            inventory.TryTake(handle, 1, out _);
            var restoreWeight = Reweigh(inventory, weaponId, item.Weight - replacedWeight);
            // 外した物は setAttachments の前に入れる。後だと、item を抜いて空いたマスを溢れた弾が使い、外した物が入らず item を戻せなくなるため
            if (replaced != null && inventory.Add(replaced, 1) != 1)
            {
                restoreWeight();
                inventory.Add(item.Id, 1);
                return AttachmentRejection.NoRoom;
            }

            if (!setAttachments(next))
            {
                if (replaced != null)
                {
                    inventory.Remove(replaced, 1);
                }

                restoreWeight();
                inventory.Add(item.Id, 1);
                return AttachmentRejection.NoRoom;
            }

            return AttachmentRejection.None;
        }

        public static AttachmentRejection Detach(
            Inventory inventory,
            string weaponId,
            IReadOnlyList<string> current,
            string itemId,
            Func<IReadOnlyList<string>, bool> setAttachments)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (setAttachments == null)
            {
                throw new ArgumentNullException(nameof(setAttachments));
            }

            if (string.IsNullOrEmpty(weaponId))
            {
                return AttachmentRejection.NoWeapon;
            }

            var next = WeaponAttachments.Detach(current, itemId);
            if (next == null || !inventory.Catalog.TryFindAttachment(itemId, out var item))
            {
                return AttachmentRejection.NotAttached;
            }

            if (inventory.RoomFor(item.Id, item.Weight) < 1)
            {
                return AttachmentRejection.NoRoom;
            }

            var restoreWeight = Reweigh(inventory, weaponId, -item.Weight);
            if (inventory.Add(item.Id, 1) != 1)
            {
                restoreWeight();
                return AttachmentRejection.NoRoom;
            }

            if (!setAttachments(next))
            {
                inventory.Remove(item.Id, 1);
                restoreWeight();
                return AttachmentRejection.NoRoom;
            }

            return AttachmentRejection.None;
        }

        // 溢れる弾の空きは setAttachments の中で、武器の重さが揃う前に判定される。付け外した後の重さで判定させるため、武器の重さを先に替える
        private static Action Reweigh(Inventory inventory, string weaponId, float delta)
        {
            if (!inventory.TryGetWeaponWeight(weaponId, out var before))
            {
                return () => { };
            }

            inventory.SetWeaponWeight(weaponId, Math.Max(0f, before + delta));
            return () => inventory.SetWeaponWeight(weaponId, before);
        }
    }
}
