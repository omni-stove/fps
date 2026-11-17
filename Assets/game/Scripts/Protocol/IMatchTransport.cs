using System;

namespace Shooter.Protocol
{
    // 受け取りは呼ぶ側の tick から TryReceive で引き出す形にし、実装のスレッドから呼び返さない。
    public interface IMatchTransport : IDisposable
    {
        bool IsActive { get; }

        void Listen(string address, ushort port);

        void Connect(string address, ushort port);

        void Disconnect(ConnectionId connection, string reason);

        void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload);

        int GetMaxPayloadSize(TransportChannel channel);

        bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds);

        bool TryReceive(out TransportEvent transportEvent);

        void Shutdown(string reason);
    }
}
