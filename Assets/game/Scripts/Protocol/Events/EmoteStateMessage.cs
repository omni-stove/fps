using System;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 始めたことと止めたことを 1 つの種類で送るのは、受け取った側が同じ人の前のエモートを必ず止めてから次を出せるよう、届く順を 1 本の流れにそろえるため。
    // 止める理由も載せるのは、相手の画面のログで「動いた」「撃った」などを見分けられるようにするため。
    public sealed class EmoteStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.EmoteState;

        public byte Version => CurrentVersion;

        public int PlayerId { get; set; }

        public bool Playing { get; set; }

        // Playing のときだけ載せる
        public string EmoteId { get; set; } = "";

        // 止めたときだけ載せる
        public EmoteStopReason StopReason { get; set; }

        public void Write(MessageWriter writer)
        {
            if (PlayerId < 0)
            {
                throw new InvalidOperationException("the player of an emote must have an id that is not negative");
            }

            if (Playing && string.IsNullOrEmpty(EmoteId))
            {
                throw new InvalidOperationException("a playing emote needs an emote id");
            }

            writer.WriteInt32(PlayerId);
            writer.WriteBool(Playing);
            if (Playing)
            {
                writer.WriteString(EmoteId, MatchProtocol.MaxContentIdBytes);
            }
            else
            {
                writer.WriteByte((byte)StopReason);
            }
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "emote player");
            Playing = reader.ReadBool();
            if (Playing)
            {
                EmoteId = reader.ReadString(MatchProtocol.MaxContentIdBytes, EmoteId);
                if (EmoteId.Length == 0)
                {
                    throw new MessageReadException("a playing emote carries no emote id");
                }

                StopReason = default;
                return;
            }

            EmoteId = "";
            StopReason = (EmoteStopReason)reader.ReadByteBelow(EnumRange<EmoteStopReason>.Count, "emote stop reason");
        }
    }
}
