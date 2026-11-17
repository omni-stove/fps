using System;

namespace Shooter.Protocol
{
    public sealed class MapVoteRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.MapVoteRequest;

        public byte Version => CurrentVersion;

        public int MapIndex { get; set; }

        public void Write(MessageWriter writer)
        {
            if ((uint)MapIndex >= MapVoteStateMessage.MaxCandidates)
            {
                throw new InvalidOperationException("map index must be below " + MapVoteStateMessage.MaxCandidates);
            }

            writer.WriteByte((byte)MapIndex);
        }

        public void Read(MessageReader reader)
        {
            MapIndex = reader.ReadByteBelow(MapVoteStateMessage.MaxCandidates, "map vote index");
        }
    }
}
