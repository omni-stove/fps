using System;

namespace Shooter.Protocol
{
    // 名前ではなく emotes.json の Id を送る。表示名は言語ごとに変わり、サーバーは Id で同じ一覧から引くため。
    public sealed class EmoteRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.EmoteRequest;

        public byte Version => CurrentVersion;

        public EmoteAction Action { get; set; }

        // Play のときだけ載せる。Stop は今のエモートを止めるので Id を持たない
        public string EmoteId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (Action == EmoteAction.Play && string.IsNullOrEmpty(EmoteId))
            {
                throw new InvalidOperationException("an emote play request needs an emote id");
            }

            if (Action == EmoteAction.Stop && !string.IsNullOrEmpty(EmoteId))
            {
                throw new InvalidOperationException("an emote stop request carries no emote id");
            }

            writer.WriteByte((byte)Action);
            if (Action == EmoteAction.Play)
            {
                writer.WriteString(EmoteId, MatchProtocol.MaxContentIdBytes);
            }
        }

        public void Read(MessageReader reader)
        {
            Action = (EmoteAction)reader.ReadByteBelow(EnumRange<EmoteAction>.Count, "emote action");
            if (Action != EmoteAction.Play)
            {
                EmoteId = "";
                return;
            }

            EmoteId = reader.ReadString(MatchProtocol.MaxContentIdBytes, EmoteId);
            if (EmoteId.Length == 0)
            {
                throw new MessageReadException("an emote play request carries no emote id");
            }
        }
    }
}
