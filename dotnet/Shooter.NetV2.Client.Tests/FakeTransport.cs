using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    /// <summary>クライアントの通信路の代わり。届く出来事をテストが積み、送ったメッセージを読み返せる。</summary>
    internal sealed class FakeTransport : IMatchTransport
    {
        public static readonly ConnectionId Server = new ConnectionId(1);

        private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();
        private readonly MessageTable _serverTable = MatchMessages.CreateServerTable();
        private readonly MessageWriter _writer = new MessageWriter();

        public bool IsActive { get; private set; }

        public List<(TransportChannel Channel, ushort Id)> Sent { get; } = new List<(TransportChannel Channel, ushort Id)>();

        public InputMessage LastInput { get; private set; }

        public bool ShutDown { get; private set; }

        public void Listen(string address, ushort port)
        {
            throw new InvalidOperationException("a client transport does not listen");
        }

        public void Connect(string address, ushort port)
        {
            IsActive = true;
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            var message = _serverTable.Read(payload);
            Sent.Add((channel, message.Id));
            if (message is InputMessage input)
            {
                var copy = new InputMessage { AckedSnapshotTick = input.AckedSnapshotTick };
                for (var i = 0; i < input.Count; i++)
                {
                    copy.Add(input[i]);
                }

                LastInput = copy;
            }
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            return MessageWriter.DefaultMaxLength;
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            milliseconds = 20;
            return true;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            if (_events.Count == 0)
            {
                transportEvent = default;
                return false;
            }

            transportEvent = _events.Dequeue();
            return true;
        }

        public void Shutdown(string reason)
        {
            ShutDown = true;
            IsActive = false;
        }

        public void Dispose()
        {
            Shutdown(null);
        }

        public void Connected()
        {
            _events.Enqueue(TransportEvent.Connected(Server));
        }

        public void Disconnected(string reason)
        {
            _events.Enqueue(TransportEvent.Disconnected(Server, reason));
        }

        public void Deliver(IMessage message)
        {
            _writer.Reset();
            if (message is SnapshotMessage snapshot)
            {
                // 差分の基準はテストの側で Baseline に入れておく
                _writer.WriteMessage(snapshot);
            }
            else
            {
                _writer.WriteMessage(message);
            }

            _events.Enqueue(TransportEvent.Data(Server, TransportChannel.Reliable, new ArraySegment<byte>(_writer.ToArray())));
        }
    }
}
