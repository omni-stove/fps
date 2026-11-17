using System;

namespace Shooter.Protocol
{
    public sealed class ChatMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Chat;

        public byte Version => CurrentVersion;

        public int SenderId { get; set; }

        public ChatChannel Channel { get; set; }

        public string Text { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (SenderId < 0)
            {
                throw new InvalidOperationException("sender id must not be negative");
            }

            if (string.IsNullOrEmpty(Text))
            {
                throw new InvalidOperationException("a chat message needs text");
            }

            writer.WriteInt32(SenderId);
            writer.WriteByte((byte)Channel);
            writer.WriteString(Text, MatchProtocol.MaxChatBytes);
        }

        public void Read(MessageReader reader)
        {
            SenderId = reader.ReadInt32AtLeast(0, "chat sender");
            Channel = (ChatChannel)reader.ReadByteBelow(EnumRange<ChatChannel>.Count, "chat channel");
            Text = reader.ReadString(MatchProtocol.MaxChatBytes, null);
            if (Text.Length == 0)
            {
                throw new MessageReadException("a chat message carries no text");
            }
        }
    }
}
