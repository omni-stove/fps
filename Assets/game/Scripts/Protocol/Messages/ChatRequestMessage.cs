using System;

namespace Shooter.Protocol
{
    public sealed class ChatRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ChatRequest;

        public byte Version => CurrentVersion;

        public ChatChannel Channel { get; set; }

        public string Text { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(Text))
            {
                throw new InvalidOperationException("a chat request needs text");
            }

            writer.WriteByte((byte)Channel);
            writer.WriteString(Text, MatchProtocol.MaxChatBytes);
        }

        public void Read(MessageReader reader)
        {
            Channel = (ChatChannel)reader.ReadByteBelow(EnumRange<ChatChannel>.Count, "chat channel");
            Text = reader.ReadString(MatchProtocol.MaxChatBytes, null);
            if (Text.Length == 0)
            {
                throw new MessageReadException("a chat request carries no text");
            }
        }
    }
}
