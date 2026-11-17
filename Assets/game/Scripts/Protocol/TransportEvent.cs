using System;

namespace Shooter.Protocol
{
    public readonly struct TransportEvent
    {
        // 理由は切断の知らせ 1 つに載せるので、どの通信路でも 1 つのパケットに収まる長さにする。
        public const int MaxReasonBytes = 255;

        private readonly string _reason;

        private TransportEvent(
            TransportEventKind kind, ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload, string reason)
        {
            Kind = kind;
            Connection = connection;
            Channel = channel;
            Payload = payload;
            _reason = reason;
        }

        public TransportEventKind Kind { get; }

        public ConnectionId Connection { get; }

        public TransportChannel Channel { get; }

        // 実装の受信用の配列を指すことがあるので、次に IMatchTransport.TryReceive を呼ぶまでしか中身を保証しない。
        public ArraySegment<byte> Payload { get; }

        // 相手が自由に送れる値なので、画面に出すなら Shooter.Core.DisconnectNotice.Classify で分けてから使う。
        public string Reason => _reason ?? "";

        public static TransportEvent Connected(ConnectionId connection)
        {
            ThrowIfNone(connection);
            return new TransportEvent(TransportEventKind.Connected, connection, default, default, null);
        }

        public static TransportEvent Disconnected(ConnectionId connection, string reason)
        {
            ThrowIfNone(connection);
            return new TransportEvent(TransportEventKind.Disconnected, connection, default, default, reason);
        }

        public static TransportEvent Data(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            ThrowIfNone(connection);
            if (!Enum.IsDefined(typeof(TransportChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown channel");
            }

            if (payload.Array == null)
            {
                throw new ArgumentException("payload must point at an array", nameof(payload));
            }

            return new TransportEvent(TransportEventKind.Data, connection, channel, payload, null);
        }

        private static void ThrowIfNone(ConnectionId connection)
        {
            if (!connection.IsValid)
            {
                throw new ArgumentException("an event needs a connection", nameof(connection));
            }
        }
    }
}
