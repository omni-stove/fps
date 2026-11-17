using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public static class InventoryAttachments
    {
        // setAttachments (武器と持ち物の武器の重さを揃える) を外した物を戻す前に呼ぶのは、武器の重さが古いままだと重さの上限で戻せないことがあるため
        public static AttachmentRejection Attach(
            Inventory inventory,
            int handle,
            string weaponId,
            IReadOnlyList<string> current,
            Action<IReadOnlyList<string>> setAttachments,
            out string itemId,
            out string replaced)
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
            if (replaced != null && catalog.TryFind(replaced, out var off)
                && !inventory.HasRoomAfterTaking(handle, replaced, Math.Max(0f, off.Weight - item.Weight)))
            {
                return AttachmentRejection.NoRoom;
            }

            inventory.TryTake(handle, 1, out _);
            setAttachments(next);
            if (replaced != null)
            {
                inventory.Add(replaced, 1);
            }

            return AttachmentRejection.None;
        }

        // Attach と同じ理由で、持ち物に戻す前に setAttachments を呼ぶ
        public static AttachmentRejection Detach(
            Inventory inventory,
            string weaponId,
            IReadOnlyList<string> current,
            string itemId,
            Action<IReadOnlyList<string>> setAttachments)
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

            // 先に外した物を入れるのは、setAttachments で溢れた弾が確かめた空きを先に使い、外した物が消えるのを防ぐため
            if (inventory.RoomFor(item.Id, item.Weight) < 1 || inventory.Add(item.Id, 1, item.Weight) != 1)
            {
                return AttachmentRejection.NoRoom;
            }

            setAttachments(next);
            return AttachmentRejection.None;
        }
    }
}
