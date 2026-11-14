using System;
using System.Globalization;
using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// <see cref="AntiCheatPacket"/> を形を変えずに運ぶのは、EOS の P2P のピア同士と同じ Hello とメッセージの手順を
    /// .NET のサーバーとの間でもそのまま使うため。向きごとに Id を分けるのは、受け取る種類を向きで分けている表
    /// (<see cref="MatchMessages"/>) に、相手が送るはずの無い向きを読ませないため。
    /// 読んだパケットを写すのは、通信路の受信用の配列が次の受け取りで上書きされるため。
    /// </summary>
    public abstract class AntiCheatMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        private readonly byte[] _packet = new byte[AntiCheatPacket.MaxPacketSize];
        private int _length;

        public abstract ushort Id { get; }

        public byte Version => CurrentVersion;

        public ArraySegment<byte> Packet => new ArraySegment<byte>(_packet, 0, _length);

        public void SetPacket(ArraySegment<byte> packet)
        {
            if (packet.Array == null || packet.Count == 0)
            {
                throw new ArgumentException("an anti-cheat packet must not be empty", nameof(packet));
            }

            if (packet.Count > AntiCheatPacket.MaxPacketSize)
            {
                throw new ArgumentException(
                    "an anti-cheat packet must be at most " + Text(AntiCheatPacket.MaxPacketSize) + " bytes (got " + Text(packet.Count) + ")",
                    nameof(packet));
            }

            Buffer.BlockCopy(packet.Array, packet.Offset, _packet, 0, packet.Count);
            _length = packet.Count;
        }

        public void Write(MessageWriter writer)
        {
            if (_length == 0)
            {
                throw new InvalidOperationException("set the anti-cheat packet before writing the message");
            }

            writer.WriteBytes(Packet);
        }

        public void Read(MessageReader reader)
        {
            var count = reader.Remaining;
            if (count == 0 || count > AntiCheatPacket.MaxPacketSize)
            {
                throw new MessageReadException(
                    "an anti-cheat packet must be 1 to " + Text(AntiCheatPacket.MaxPacketSize) + " bytes (got " + Text(count) + ")");
            }

            var bytes = reader.ReadBytes(count);
            Buffer.BlockCopy(bytes.Array, bytes.Offset, _packet, 0, count);
            _length = count;
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class AntiCheatToServerMessage : AntiCheatMessage
    {
        public override ushort Id => MessageIds.AntiCheatToServer;
    }

    public sealed class AntiCheatToClientMessage : AntiCheatMessage
    {
        public override ushort Id => MessageIds.AntiCheatToClient;
    }
}
