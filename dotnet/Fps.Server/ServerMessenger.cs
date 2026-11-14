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

        public void Send(ConnectionId connection, TransportChannel channel, IMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            _writer.Reset();
            _writer.WriteMessage(message);
            _transport.Send(connection, channel, _writer.AsSegment());
        }
    }
}
