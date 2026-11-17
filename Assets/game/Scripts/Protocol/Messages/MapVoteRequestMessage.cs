using System;

namespace Shooter.Protocol
{
    /// <summary>
    /// 持ち主が次のマップに <see cref="MapIndex"/> 番目の候補 (<see cref="MapVoteStateMessage"/> の並び) を選ぶ。
    /// 候補の数より小さいか・投票中かはサーバーが確かめる。
    /// </summary>
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
