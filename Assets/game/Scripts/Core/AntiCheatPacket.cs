using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct AntiCheatPacket
    {
        public const byte ProtocolVersion = 1;

        // EAC の SDK が出すメッセージは P2P では 256 バイト以下なので、余裕を持たせて 512 にする。
        public const int MaxPayloadSize = 512;

        public const int HelloSize = 3;

        public const int MaxPacketSize = 1 + MaxPayloadSize;

        private AntiCheatPacket(AntiCheatPacketKind kind, bool peerEnabled, ArraySegment<byte> payload)
        {
            Kind = kind;
            PeerEnabled = peerEnabled;
            Payload = payload;
        }

        public AntiCheatPacketKind Kind { get; }

        public bool PeerEnabled { get; }

        public ArraySegment<byte> Payload { get; }

        public static int WriteHello(byte[] buffer, bool enabled)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length < HelloSize)
            {
                throw new ArgumentException("buffer is too small for a hello", nameof(buffer));
            }

            buffer[0] = (byte)AntiCheatPacketKind.Hello;
            buffer[1] = ProtocolVersion;
            buffer[2] = enabled ? (byte)1 : (byte)0;
            return HelloSize;
        }

        public static int WriteMessage(byte[] buffer, ArraySegment<byte> payload)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (payload.Array == null || payload.Count == 0)
            {
                throw new ArgumentException("an anti-cheat message must not be empty", nameof(payload));
            }

            if (payload.Count > MaxPayloadSize)
            {
                throw new ArgumentException(
                    "an anti-cheat message must be at most " + Text(MaxPayloadSize) + " bytes (got " + Text(payload.Count) + ")",
                    nameof(payload));
            }

            if (buffer.Length < 1 + payload.Count)
            {
                throw new ArgumentException("buffer is too small for the message", nameof(buffer));
            }

            buffer[0] = (byte)AntiCheatPacketKind.Message;
            Buffer.BlockCopy(payload.Array, payload.Offset, buffer, 1, payload.Count);
            return 1 + payload.Count;
        }

        public static AntiCheatPacket Read(ArraySegment<byte> packet, out string error)
        {
            if (packet.Array == null || packet.Count == 0)
            {
                error = "empty packet";
                return default;
            }

            var bytes = packet.Array;
            var start = packet.Offset;
            switch ((AntiCheatPacketKind)bytes[start])
            {
                case AntiCheatPacketKind.Hello:
                    // 版が違えば後ろの形も違いうるので、長さより先に版を見る
                    if (packet.Count >= 2 && bytes[start + 1] != ProtocolVersion)
                    {
                        error = "protocol version " + Text(bytes[start + 1]) + " is not " + Text(ProtocolVersion);
                        return default;
                    }

                    if (packet.Count != HelloSize)
                    {
                        error = "hello must be " + Text(HelloSize) + " bytes (got " + Text(packet.Count) + ")";
                        return default;
                    }

                    var flag = bytes[start + 2];
                    if (flag > 1)
                    {
                        error = "hello has an unknown enabled flag " + Text(flag);
                        return default;
                    }

                    error = null;
                    return new AntiCheatPacket(AntiCheatPacketKind.Hello, flag == 1, default);
                case AntiCheatPacketKind.Message:
                    var size = packet.Count - 1;
                    if (size == 0 || size > MaxPayloadSize)
                    {
                        error = "message must be 1 to " + Text(MaxPayloadSize) + " bytes (got " + Text(size) + ")";
                        return default;
                    }

                    error = null;
                    return new AntiCheatPacket(
                        AntiCheatPacketKind.Message, false, new ArraySegment<byte>(bytes, start + 1, size));
                default:
                    error = "unknown packet kind " + Text(bytes[start]);
                    return default;
            }
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
