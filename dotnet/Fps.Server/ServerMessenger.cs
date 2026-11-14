using System;
using Fps.Protocol;

namespace Fps.Server
{
    internal sealed class ServerMessenger
    {
        private readonly IMatchTransport _transport;
        private readonly MessageWriter _writer = new MessageWriter();

        public ServerMessenger(IMatchTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        // Bot は接続を持たない (ConnectionId.None) まま試合の一覧にいるので、全員へ送る側が Bot を見分けなくて済むよう、ここで読み飛ばす
        public void Send(ConnectionId connection, TransportChannel channel, IMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (!connection.IsValid)
            {
                return;
            }

            _writer.Reset();
            _writer.WriteMessage(message);
            _transport.Send(connection, channel, _writer.AsSegment());
        }

        /// <summary>
        /// 落ちてよい届け方に収まらない大きさ (基準の無いスナップショットに大勢が載るなど) なら、分けて送れる届く方の届け方で送る。
        /// </summary>
        public TransportChannel SendPreferringUnreliable(ConnectionId connection, IMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (!connection.IsValid)
            {
                return TransportChannel.Unreliable;
            }

            _writer.Reset();
            _writer.WriteMessage(message);
            var channel = _writer.Length <= _transport.GetMaxPayloadSize(TransportChannel.Unreliable)
                ? TransportChannel.Unreliable
                : TransportChannel.Reliable;
            _transport.Send(connection, channel, _writer.AsSegment());
            return channel;
        }
    }
}
