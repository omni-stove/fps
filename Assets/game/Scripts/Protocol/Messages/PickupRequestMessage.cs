namespace Shooter.Protocol
{
    // 拾い物を拾う・弾薬箱を使う要求。開ける要求 (InteractRequestMessage) と分けるのは、箱は目的 (BR・Extraction) が開けてよいかを決め、
    // 拾い物はどのモードでも拾える物として別の部品 (サーバーの ServerPickups) が受けるため。
    // 位置を送らないのは、届く距離にあるかはサーバーが自分の物の位置で決め、クライアントの言い値を信じないため。
    public sealed class PickupRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PickupRequest;

        public byte Version => CurrentVersion;

        // 拾う物か使う弾薬箱 (ObjectiveItemSnapshot.ItemId)
        public int ItemId { get; set; }

        public void Write(MessageWriter writer)
        {
            if (ItemId < 0)
            {
                throw new System.InvalidOperationException("the pickup to collect must not have a negative id");
            }

            writer.WriteInt32(ItemId);
        }

        public void Read(MessageReader reader)
        {
            ItemId = reader.ReadInt32AtLeast(0, "pickup item");
        }
    }
}
