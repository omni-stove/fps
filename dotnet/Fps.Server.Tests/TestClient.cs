using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Fps.Protocol;

namespace Fps.Server.Tests
{
    internal sealed class TestClient : IDisposable
    {
        private readonly UdpMatchTransport _transport = new UdpMatchTransport();
        private readonly MessageTable _table = MatchMessages.CreateClientTable();
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly Dictionary<int, SnapshotMessage> _snapshots = new Dictionary<int, SnapshotMessage>();
        private ConnectionId _server;

        public bool Connected { get; private set; }

        public bool Disconnected { get; private set; }

        public string DisconnectReason { get; private set; }

        public List<ushort> Received { get; } = new List<ushort>();

        public ConnectAcceptedMessage Accepted { get; private set; }

        public string RejectedReason { get; private set; }

        public string ServerDisconnectingReason { get; private set; }

        /// <summary>最後に組み立てられた (差分なら基準で埋めた) スナップショット。</summary>
        public SnapshotMessage LatestSnapshot { get; private set; }

        public int UnresolvedSnapshots { get; private set; }

        /// <summary>組み立てられたスナップショットのうち、差分で届いたものの数。</summary>
        public int DeltaSnapshots { get; private set; }

        public static ushort FreePort()
        {
            using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                return (ushort)((IPEndPoint)probe.Client.LocalEndPoint).Port;
            }
        }

        public void Connect(ushort port)
        {
            _transport.Connect("127.0.0.1", port);
        }

        public void Send(IMessage message)
        {
            _writer.Reset();
            _writer.WriteMessage(message);
            _transport.Send(_server, TransportChannel.Reliable, _writer.AsSegment());
        }

        public void Send(IMessage message, TransportChannel channel)
        {
            _writer.Reset();
            _writer.WriteMessage(message);
            _transport.Send(_server, channel, _writer.AsSegment());
        }

        public void SendRaw(byte[] bytes)
        {
            _transport.Send(_server, TransportChannel.Reliable, new ArraySegment<byte>(bytes));
        }

        public void SendRequest(string version, string key = "device1", ushort protocol = MatchProtocol.Version)
        {
            Send(new ConnectRequestMessage { ClientVersion = version, RejoinKey = key, DisplayName = "tester", ProtocolVersion = protocol });
        }

        public void Poll()
        {
            while (_transport.TryReceive(out var e))
            {
                switch (e.Kind)
                {
                    case TransportEventKind.Connected:
                        _server = e.Connection;
                        Connected = true;
                        break;
                    case TransportEventKind.Disconnected:
                        Disconnected = true;
                        DisconnectReason = e.Reason;
                        break;
                    default:
                        Read(e.Payload);
                        break;
                }
            }
        }

        public void Dispose()
        {
            _transport.Dispose();
        }

        private void Read(ArraySegment<byte> payload)
        {
            var message = _table.Read(payload);
            Received.Add(message.Id);
            switch (message)
            {
                case ConnectAcceptedMessage accepted:
                    Accepted = new ConnectAcceptedMessage
                    {
                        PlayerId = accepted.PlayerId,
                        ServerVersion = accepted.ServerVersion,
                        TickRate = accepted.TickRate,
                        ServerTick = accepted.ServerTick,
                    };
                    break;
                case ConnectRejectedMessage rejected:
                    RejectedReason = rejected.Reason;
                    break;
                case ServerDisconnectingMessage disconnecting:
                    ServerDisconnectingReason = disconnecting.Reason;
                    break;
                case SnapshotMessage snapshot:
                    Keep(snapshot);
                    break;
            }
        }

        private void Keep(SnapshotMessage snapshot)
        {
            var delta = snapshot.BaselineTick != SnapshotMessage.NoBaseline;
            _snapshots.TryGetValue(snapshot.BaselineTick, out var baseline);
            if (!snapshot.TryResolve(baseline))
            {
                UnresolvedSnapshots++;
                return;
            }

            if (delta)
            {
                DeltaSnapshots++;
            }

            var kept = new SnapshotMessage();
            kept.CopyFrom(snapshot);
            _snapshots[kept.Tick] = kept;
            _snapshots.Remove(kept.Tick - 64);
            if (LatestSnapshot == null || kept.Tick > LatestSnapshot.Tick)
            {
                LatestSnapshot = kept;
            }
        }
    }
}
