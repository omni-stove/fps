using System;

namespace Fps.Protocol
{
    /// <summary><see cref="IMatchTransport.TryReceive"/> が返す 1 つの出来事。</summary>
    public readonly struct TransportEvent
    {
        /// <summary>
        /// 切断の理由の最長のバイト数 (UTF-8)。理由は切断の知らせ 1 つに載せるので、どの通信路でも 1 つのパケットに収まる長さにする。
        /// </summary>
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

        /// <summary>出来事が起きた接続。</summary>
        public ConnectionId Connection { get; }

        /// <summary><see cref="TransportEventKind.Data"/> のとき、届いたチャンネル。</summary>
        public TransportChannel Channel { get; }

        /// <summary>
        /// <see cref="TransportEventKind.Data"/> のとき、届いたデータ。実装の受信用の配列を指すことがあり、
        /// 次に <see cref="IMatchTransport.TryReceive"/> を呼ぶまでしか中身を保証しない。それ以外では空。
        /// </summary>
        public ArraySegment<byte> Payload { get; }

        /// <summary>
        /// <see cref="TransportEventKind.Disconnected"/> のとき、切った側が <see cref="IMatchTransport.Disconnect"/> か
        /// <see cref="IMatchTransport.Shutdown"/> に渡した理由。自分から切ったなら自分が渡した理由。
        /// 知らせが無い (相手が黙って消えた・つながらなかった) ときや、それ以外の出来事では空文字。
        /// 相手が自由に送れる値なので、画面に出すなら <c>Fps.Core.DisconnectNotice.Classify</c> で分けてから使う。
        /// </summary>
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
