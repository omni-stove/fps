using System;
using System.Collections.Generic;

namespace Fps.Protocol
{
    // 大きさと重さも送るのは、クライアントがサーバーと違うアイテムの表を持っていても並べて描けるようにするため
    public sealed class InventoryStateMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public const int MaxCount = byte.MaxValue;

        private Item[] _items = Array.Empty<Item>();
        private Entry[] _entries = Array.Empty<Entry>();
        private byte[] _attached = Array.Empty<byte>();
        private int _attachedCount;

        public ushort Id => MessageIds.InventoryState;

        public byte Version => CurrentVersion;

        public byte MaxStacks { get; set; }

        public float MaxWeight { get; set; }

        public byte GridWidth { get; set; }

        public byte GridHeight { get; set; }

        public bool AllowRotation { get; set; }

        public int ItemCount { get; private set; }

        public int EntryCount { get; private set; }

        public void Clear()
        {
            ItemCount = 0;
            EntryCount = 0;
            _attachedCount = 0;
        }

        public Item ItemAt(int index)
        {
            if ((uint)index >= (uint)ItemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + ItemCount);
            }

            return _items[index];
        }

        public Entry EntryAt(int index)
        {
            if ((uint)index >= (uint)EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + EntryCount);
            }

            return _entries[index];
        }

        public int AddItem(Item item)
        {
            if (ItemCount == MaxCount)
            {
                throw new InvalidOperationException("at most " + MaxCount + " inventory items fit");
            }

            Grow(ref _items, ItemCount + 1);
            _items[ItemCount] = item;
            return ItemCount++;
        }

        public void AddEntry(Entry entry)
        {
            AddEntry(entry, Array.Empty<byte>());
        }

        public void AddEntry(Entry entry, IReadOnlyList<byte> attachedItems)
        {
            if (EntryCount == MaxCount)
            {
                throw new InvalidOperationException("at most " + MaxCount + " inventory entries fit");
            }

            if (attachedItems == null)
            {
                throw new ArgumentNullException(nameof(attachedItems));
            }

            if (attachedItems.Count > MaxCount)
            {
                throw new ArgumentException("at most " + MaxCount + " attached items fit on one entry", nameof(attachedItems));
            }

            Grow(ref _entries, EntryCount + 1);
            Grow(ref _attached, _attachedCount + attachedItems.Count);
            var start = _attachedCount;
            foreach (var item in attachedItems)
            {
                _attached[_attachedCount++] = item;
            }

            _entries[EntryCount++] = entry.WithAttached(start, attachedItems.Count);
        }

        public byte AttachedItemAt(int entryIndex, int index)
        {
            var entry = EntryAt(entryIndex);
            if ((uint)index >= (uint)entry.AttachedCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + entry.AttachedCount);
            }

            return _attached[entry.AttachedStart + index];
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte(MaxStacks);
            writer.WriteSingle(MaxWeight);
            writer.WriteByte(GridWidth);
            writer.WriteByte(GridHeight);
            writer.WriteBool(AllowRotation);
            writer.WriteByte((byte)ItemCount);
            for (var i = 0; i < ItemCount; i++)
            {
                var item = _items[i];
                writer.WriteString(item.ItemId, MatchProtocol.MaxContentIdBytes);
                writer.WriteUInt16(item.MaxStack);
                writer.WriteSingle(item.Weight);
                writer.WriteByte(item.Width);
                writer.WriteByte(item.Height);
            }

            writer.WriteByte((byte)EntryCount);
            for (var i = 0; i < EntryCount; i++)
            {
                var entry = _entries[i];
                writer.WriteUInt16(entry.Handle);
                writer.WriteByte(entry.ItemIndex);
                writer.WriteUInt16(entry.Count);
                writer.WriteByte(entry.X);
                writer.WriteByte(entry.Y);
                writer.WriteBool(entry.Rotated);
                writer.WriteByte((byte)entry.AttachedCount);
                for (var a = 0; a < entry.AttachedCount; a++)
                {
                    writer.WriteByte(_attached[entry.AttachedStart + a]);
                }
            }
        }

        public void Read(MessageReader reader)
        {
            MaxStacks = reader.ReadByte();
            MaxWeight = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "inventory max weight");
            GridWidth = reader.ReadByte();
            GridHeight = reader.ReadByte();
            AllowRotation = reader.ReadBool();
            var itemCount = reader.ReadByte();
            Grow(ref _items, itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                var id = reader.ReadString(MatchProtocol.MaxContentIdBytes, i < ItemCount ? _items[i].ItemId : null);
                var maxStack = reader.ReadUInt16();
                var weight = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "inventory item weight");
                var width = reader.ReadByte();
                var height = reader.ReadByte();
                _items[i] = new Item(id, maxStack, weight, width, height);
            }

            ItemCount = itemCount;
            var entryCount = reader.ReadByte();
            Grow(ref _entries, entryCount);
            _attachedCount = 0;
            for (var i = 0; i < entryCount; i++)
            {
                var handle = reader.ReadUInt16();
                var itemIndex = reader.ReadByteBelow(itemCount, "inventory entry item");
                var count = reader.ReadUInt16();
                var x = reader.ReadByte();
                var y = reader.ReadByte();
                var rotated = reader.ReadBool();
                var attachedCount = reader.ReadByte();
                var start = _attachedCount;
                Grow(ref _attached, _attachedCount + attachedCount);
                for (var a = 0; a < attachedCount; a++)
                {
                    _attached[_attachedCount++] = reader.ReadByteBelow(itemCount, "inventory entry attached item");
                }

                _entries[i] = new Entry(handle, itemIndex, count, x, y, rotated).WithAttached(start, attachedCount);
            }

            EntryCount = entryCount;
        }

        private static void Grow<T>(ref T[] array, int count)
        {
            if (array.Length < count)
            {
                Array.Resize(ref array, Math.Max(count, array.Length * 2));
            }
        }

        public readonly struct Item
        {
            public Item(string itemId, ushort maxStack, float weight, byte width, byte height)
            {
                ItemId = itemId ?? "";
                MaxStack = maxStack;
                Weight = weight;
                Width = width;
                Height = height;
            }

            public string ItemId { get; }

            public ushort MaxStack { get; }

            public float Weight { get; }

            public byte Width { get; }

            public byte Height { get; }
        }

        public readonly struct Entry
        {
            public Entry(ushort handle, byte itemIndex, ushort count, byte x, byte y, bool rotated)
                : this(handle, itemIndex, count, x, y, rotated, 0, 0)
            {
            }

            private Entry(ushort handle, byte itemIndex, ushort count, byte x, byte y, bool rotated, int attachedStart, int attachedCount)
            {
                AttachedStart = attachedStart;
                AttachedCount = attachedCount;
                Handle = handle;
                ItemIndex = itemIndex;
                Count = count;
                X = x;
                Y = y;
                Rotated = rotated;
            }

            public ushort Handle { get; }

            public byte ItemIndex { get; }

            public ushort Count { get; }

            public byte X { get; }

            public byte Y { get; }

            public bool Rotated { get; }

            public int AttachedStart { get; }

            public int AttachedCount { get; }

            public Entry WithAttached(int start, int count) => new Entry(Handle, ItemIndex, Count, X, Y, Rotated, start, count);
        }
    }
}
