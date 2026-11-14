using System;
using System.Collections.Generic;
using Fps.Protocol;

namespace Fps.Server.Tests
{
    /// <summary>送ったものを覚えておくだけの通路。届いたものは無い。</summary>
    internal sealed class RecordingTransport : IMatchTransport
    {
        public const int UnreliableLimit = 1200;

        public List<(ConnectionId Connection, TransportChannel Channel, byte[] Payload)> Sent { get; } =
            new List<(ConnectionId Connection, TransportChannel Channel, byte[] Payload)>();

        public bool IsActive => true;

        public void Listen(string address, ushort port)
        {
        }

        public void Connect(string address, ushort port)
        {
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            Sent.Add((connection, channel, payload.ToArray()));
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            return channel == TransportChannel.Unreliable ? UnreliableLimit : ushort.MaxValue;
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            milliseconds = 0;
            return false;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            transportEvent = default;
            return false;
        }

        public void Shutdown(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
