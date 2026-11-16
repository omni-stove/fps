using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Protocol
{
    public static class InventorySync
    {
        public static void Write(Inventory inventory, InventoryStateMessage message)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var rule = inventory.Rule;
            message.Clear();
            message.MaxStacks = (byte)rule.MaxStacks;
            message.MaxWeight = rule.MaxWeight;
            message.GridWidth = (byte)rule.GridWidth;
            message.GridHeight = (byte)rule.GridHeight;
            message.AllowRotation = rule.AllowRotation;
            foreach (var entry in inventory.Stacks)
            {
                if (message.EntryCount == InventoryStateMessage.MaxCount || !inventory.Catalog.TryFind(entry.ItemId, out var item))
                {
                    continue;
                }

                var itemIndex = IndexOf(message, item.Id);
                if (itemIndex < 0)
                {
                    itemIndex = message.AddItem(new InventoryStateMessage.Item(
                        item.Id, (ushort)item.MaxStack, item.Weight, (byte)item.Width, (byte)item.Height));
                }

                message.AddEntry(new InventoryStateMessage.Entry(
                    (ushort)entry.Handle, (byte)itemIndex, (ushort)Math.Min(entry.Count, ushort.MaxValue), (byte)Math.Min(entry.X, byte.MaxValue),
                    (byte)Math.Min(entry.Y, byte.MaxValue), entry.Rotated));
            }
        }

        public static ItemCatalog ToCatalog(InventoryStateMessage message, ItemCatalog local)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            local = local ?? ItemCatalog.Empty;
            InventoryRule rule;
            try
            {
                rule = new InventoryRule(
                    message.MaxStacks, message.MaxWeight, message.GridWidth, message.GridHeight, message.AllowRotation, local.Rule.CountWeightInCarryLoad);
            }
            catch (ArgumentException)
            {
                rule = InventoryRule.Default;
            }

            var items = new List<ItemSpec>(message.ItemCount);
            for (var i = 0; i < message.ItemCount; i++)
            {
                var sent = message.ItemAt(i);
                if (IndexIn(items, sent.ItemId) >= 0)
                {
                    continue;
                }

                local.TryFind(sent.ItemId, out var known);
                try
                {
                    items.Add(new ItemSpec(
                        sent.ItemId,
                        known?.NameKey,
                        known?.IconId,
                        Math.Max(1, (int)sent.MaxStack),
                        known?.MaxCarried ?? 0,
                        sent.Weight,
                        sent.Width,
                        sent.Height,
                        known?.UseHealth ?? 0,
                        known?.UseArmor ?? 0));
                }
                catch (ArgumentException)
                {
                    // 読めないアイテムは山ごと出さない
                }
            }

            return new ItemCatalog(items, rule);
        }

        public static List<InventoryEntry> ToEntries(InventoryStateMessage message, ItemCatalog catalog)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var entries = new List<InventoryEntry>(message.EntryCount);
            for (var i = 0; i < message.EntryCount; i++)
            {
                var sent = message.EntryAt(i);
                var itemId = message.ItemAt(sent.ItemIndex).ItemId;
                if (sent.Handle == 0 || sent.Count == 0 || catalog.IndexOf(itemId) < 0 || HasHandle(entries, sent.Handle))
                {
                    continue;
                }

                entries.Add(new InventoryEntry(sent.Handle, itemId, sent.Count, sent.X, sent.Y, sent.Rotated));
            }

            return entries;
        }

        private static int IndexOf(InventoryStateMessage message, string itemId)
        {
            for (var i = 0; i < message.ItemCount; i++)
            {
                if (string.Equals(message.ItemAt(i).ItemId, itemId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexIn(List<ItemSpec> items, string itemId)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, itemId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasHandle(List<InventoryEntry> entries, int handle)
        {
            foreach (var entry in entries)
            {
                if (entry.Handle == handle)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
