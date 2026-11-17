namespace Shooter.Protocol
{
    public sealed class InventoryRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        // InventoryAction の値の数。読むときにこれ未満かを確かめる
        private const int _actionCount = 5;

        public ushort Id => MessageIds.InventoryRequest;

        public byte Version => CurrentVersion;

        public InventoryAction Action { get; set; }

        public ushort Handle { get; set; }

        public byte X { get; set; }

        public byte Y { get; set; }

        public bool Rotated { get; set; }

        public ushort Count { get; set; }

        public string ItemId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Action);
            writer.WriteUInt16(Handle);
            writer.WriteByte(X);
            writer.WriteByte(Y);
            writer.WriteBool(Rotated);
            writer.WriteUInt16(Count);
            writer.WriteString(ItemId ?? "", MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            Action = (InventoryAction)reader.ReadByteBelow(_actionCount, "inventory action");
            Handle = reader.ReadUInt16();
            X = reader.ReadByte();
            Y = reader.ReadByte();
            Rotated = reader.ReadBool();
            Count = reader.ReadUInt16();
            ItemId = reader.ReadString(MatchProtocol.MaxContentIdBytes, ItemId);
        }
    }
}
