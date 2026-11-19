namespace Shooter.Protocol
{
    // 位置を送らないのは、届く距離にあるかはサーバーが自分の物の位置 (ObjectiveItemSnapshot.Radius) で決め、クライアントの言い値を信じないため。
    public sealed class InteractRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.InteractRequest;

        public byte Version => CurrentVersion;

        // 開ける箱の物 (ObjectiveItemSnapshot.ItemId)
        public int ItemId { get; set; }

        public void Write(MessageWriter writer)
        {
            if (ItemId < 0)
            {
                throw new System.InvalidOperationException("the item to interact with must not have a negative id");
            }

            writer.WriteInt32(ItemId);
        }

        public void Read(MessageReader reader)
        {
            ItemId = reader.ReadInt32AtLeast(0, "interact item");
        }
    }
}
