using System;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 買えたかは届くお金の変化でも分かるが、持てない (CannotCarry) などの断りはお金が動かず分からないので、要求を送った人にだけ答えを返す。
    // 品の Id は送ってきた要求の Id をそのまま返す。同じ人に返すだけなので、一覧に無い Id でも害は無く、受けた側が待っている品と照らせるため。
    public sealed class BuyAnswerMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.BuyAnswer;

        public byte Version => CurrentVersion;

        public string ItemId { get; set; } = "";

        public PurchaseVerdict Verdict { get; set; }

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(ItemId))
            {
                throw new InvalidOperationException("a buy answer needs the item id of the request");
            }

            if ((uint)Verdict >= (uint)EnumRange<PurchaseVerdict>.Count)
            {
                throw new InvalidOperationException("unknown purchase verdict " + (int)Verdict);
            }

            writer.WriteString(ItemId, MatchProtocol.MaxContentIdBytes);
            writer.WriteByte((byte)Verdict);
        }

        public void Read(MessageReader reader)
        {
            ItemId = reader.ReadString(MatchProtocol.MaxContentIdBytes, ItemId);
            if (ItemId.Length == 0)
            {
                throw new MessageReadException("a buy answer carries no item id");
            }

            Verdict = (PurchaseVerdict)reader.ReadByteBelow(EnumRange<PurchaseVerdict>.Count, "purchase verdict");
        }
    }
}
