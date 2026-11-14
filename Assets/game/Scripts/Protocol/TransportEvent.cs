using System;

namespace Fps.Protocol
{
    /// <summary><see cref="IMatchTransport.TryReceive"/> が返す 1 つの出来事。</summary>
    public readonly struct TransportEvent
    {
        private TransportEvent(TransportEventKind kind, ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            Kind = kind;
            Connection = connection;
            Channel = channel;
            Payload = payload;
        }

        public TransportEventKind Kind { get; }

        /// <summary>出来事が起きた接続。</summary>
        public ConnectionId Connection { get; }

        /// <summary><see cref="TransportEventKind.Data"/> のとき、届いたチャンネル。</summary>
        public TransportChannel Channel { get; }

        /// <summary>
        /// <see cref="TransportEventKind.Data"/> のとき、届いたデータ。実装の受信用の配列を指すことがあり、
        /// 次に <see cref="IMatchTransport.TryReceive"/> を呼ぶまでしか中身を保証しない。それ以外では空。
        /// </summary>
        public ArraySegment<byte> Payload { get; }

        public static TransportEvent Connected(ConnectionId connection)
        {
            ThrowIfNone(connection);
            return new TransportEvent(TransportEventKind.Connected, connection, default, default);
        }

        public static TransportEvent Disconnected(ConnectionId connection)
        {
            ThrowIfNone(connection);
            return new TransportEvent(TransportEventKind.Disconnected, connection, default, default);
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

            return new TransportEvent(TransportEventKind.Data, connection, channel, payload);
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
