using System;
using System.Collections.Generic;

namespace Fps.Core
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
            // 外した物は setAttachments の前に入れる。後だと、item を抜いて空いたマスを溢れた弾が使い、外した物が入らず item を戻せなくなるため
            var returned = false;
            if (replaced != null && catalog.TryFind(replaced, out var removed))
            {
                if (inventory.Add(replaced, 1, removed.Weight) != 1)
                {
                    inventory.Add(item.Id, 1, 0f);
                    return AttachmentRejection.NoRoom;
                }

                returned = true;
            }

            // 武器が断るのは、弾倉が減って溢れる弾が持ち物に入らないとき。付ける前の持ち物に戻す
            if (!setAttachments(next))
            {
                if (returned)
                {
                    inventory.Remove(replaced, 1);
                }

                inventory.Add(item.Id, 1, 0f);
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

            // 外した物は setAttachments の後に入れる。先に入れると、武器の重さにも数えられた同じ物で空きが減り、溢れる弾の判定が厳しくなるため
            if (!setAttachments(next))
            {
                return AttachmentRejection.NoRoom;
            }

            if (inventory.Add(item.Id, 1, item.Weight) != 1)
            {
                setAttachments(current);
                return AttachmentRejection.NoRoom;
            }

            return AttachmentRejection.None;
        }
    }
}
