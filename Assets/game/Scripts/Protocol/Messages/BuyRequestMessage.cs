using System;

namespace Shooter.Protocol
{
    // 値段を送らないのは、値段と買えるかどうかはサーバーが自分のデータで決め、クライアントの言い値を信じないため。
    public sealed class BuyRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.BuyRequest;

        public byte Version => CurrentVersion;

        public string ItemId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(ItemId))
            {
                throw new InvalidOperationException("a buy request needs an item id");
            }

            writer.WriteString(ItemId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            ItemId = reader.ReadString(MatchProtocol.MaxContentIdBytes, ItemId);
            if (ItemId.Length == 0)
            {
                throw new MessageReadException("a buy request carries no item id");
            }
        }
    }
}
