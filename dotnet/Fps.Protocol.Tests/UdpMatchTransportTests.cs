using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    /// <summary>同じプロセスの中で、ループバックの UDP でサーバーとクライアントをつなぐ。</summary>
    [NonParallelizable]
    public sealed class UdpMatchTransportTests
    {
        private const string _loopback ="127.0.0.1";
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private readonly List<UdpMatchTransport> _transports = new List<UdpMatchTransport>();

        [TearDown]
        public void TearDown()
        {
            foreach (var transport in _transports)
            {
                transport.Dispose();
            }

            _transports.Clear();
        }

        [Test]
        public void ClientAndServerSeeEachOtherConnect()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            Assert.That(server.IsActive, Is.True);
            Assert.That(client.IsActive, Is.True);
            Assert.That(serverSide.IsValid, Is.True);
            Assert.That(clientSide.IsValid, Is.True);
        }

        [Test]
        public void ReliableMessagesArriveInOrderOnTheReliableChannel()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            for (byte i = 1; i <= 50; i++)
            {
                client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { i, (byte)(i * 2) }));
            }

            var received = new List<byte[]>();
            Pump(() => received.Count == 50, new Collector(server, received, serverSide, TransportChannel.Reliable), client);

            for (var i = 0; i < 50; i++)
            {
                Assert.That(received[i], Is.EqualTo(new[] { (byte)(i + 1), (byte)((i + 1) * 2) }));
            }
        }

        [Test]
        public void ReliableMessageLargerThanOnePacketArrivesWhole()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var payload = new byte[UdpMatchTransport.MaxReliablePayloadSize];
            new Random(7).NextBytes(payload);

            server.Send(serverSide, TransportChannel.Reliable, new ArraySegment<byte>(payload));

            var received = new List<byte[]>();
            Pump(() => received.Count == 1, new Collector(client, received, clientSide, TransportChannel.Reliable), server);
            Assert.That(received[0], Is.EqualTo(payload));
        }

        [Test]
        public void UnreliableMessagesArriveOnTheUnreliableChannel()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var payload = new byte[UdpMatchTransport.MaxUnreliablePayloadSize];
            payload[0] = 42;
            payload[payload.Length - 1] = 24;

            var received = new List<byte[]>();
            var collector = new Collector(client, received, clientSide, TransportChannel.Unreliable);
            // 落ちてもよいチャンネルなので、届くまで送り直す。
            Pump(
                () =>
                {
                    server.Send(serverSide, TransportChannel.Unreliable, new ArraySegment<byte>(payload));
                    return received.Count > 0;
                },
                collector,
                server);

            Assert.That(received[0], Is.EqualTo(payload));
        }

        [Test]
        public void PayloadIsCopiedWhenSent()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var buffer = new byte[] { 1, 2, 3 };

            client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(buffer, 1, 2));
            buffer[1] = 99;

            var received = new List<byte[]>();
            Pump(() => received.Count == 1, new Collector(server, received, serverSide, TransportChannel.Reliable), client);
            Assert.That(received[0], Is.EqualTo(new byte[] { 2, 3 }));
        }

        [Test]
        public void RejectionReasonReachesTheClient()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var reason = DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail);

            server.Disconnect(serverSide, reason);

            var serverEvent = WaitFor(server, TransportEventKind.Disconnected);
            Assert.That(serverEvent.Connection, Is.EqualTo(serverSide));
            Assert.That(serverEvent.Reason, Is.EqualTo(reason));

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected);
            Assert.That(clientEvent.Connection, Is.EqualTo(clientSide));
            Assert.That(clientEvent.Reason, Is.EqualTo(reason));
            var notice = DisconnectNotice.Classify(clientEvent.Reason, true, null);
            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(notice.Message.Key, Is.EqualTo(DisconnectNotice.ServerDrainingMessage.Key));
            Assert.That(client.IsActive, Is.False);
        }

        [Test]
        public void VersionMismatchReasonKeepsTheServerVersion()
        {
            var (server, client, serverSide, _) = ConnectPair();

            server.Disconnect(serverSide, DisconnectNotice.VersionMismatchReason("0.2.0"));

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected, server);
            var notice = DisconnectNotice.Classify(clientEvent.Reason, false, "0.1.0");
            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(notice.ServerVersion, Is.EqualTo("0.2.0"));
        }

        [Test]
        public void ServerShutdownReachesTheClientAsServerStopped()
        {
            var (server, client, _, clientSide) = ConnectPair();

            server.Shutdown(DisconnectNotice.ServerStoppedReason);

            Assert.That(server.IsActive, Is.False);
            Assert.That(server.TryReceive(out _), Is.False);
            var clientEvent = WaitFor(client, TransportEventKind.Disconnected);
            Assert.That(clientEvent.Connection, Is.EqualTo(clientSide));
            Assert.That(DisconnectNotice.Classify(clientEvent.Reason, true, null).Kind, Is.EqualTo(DisconnectKind.ServerStopped));
        }

        [Test]
        public void ClientLeavingTellsTheServerItsReason()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            client.Disconnect(clientSide, "left");

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected);
            Assert.That(clientEvent.Connection, Is.EqualTo(clientSide));
            Assert.That(clientEvent.Reason, Is.EqualTo("left"));
            var serverEvent = WaitFor(server, TransportEventKind.Disconnected);
            Assert.That(serverEvent.Connection, Is.EqualTo(serverSide));
            Assert.That(serverEvent.Reason, Is.EqualTo("left"));
        }

        [Test]
        public void ConnectingToNobodyEndsWithoutAReason()
        {
            var options = new UdpTransportOptions { ConnectAttemptIntervalMilliseconds = 20, MaxConnectAttempts = 2 };
            var client = Track(new UdpMatchTransport(options));
            var unused = Track(new UdpMatchTransport());
            unused.Listen(_loopback,0);
            var port = (ushort)unused.LocalPort;
            unused.Shutdown(null);

            client.Connect(_loopback,port);

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected);
            Assert.That(clientEvent.Reason, Is.Empty);
            var notice = DisconnectNotice.Classify(clientEvent.Reason, false, null);
            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.TimedOut));
            Assert.That(notice.Message.Key, Is.EqualTo(DisconnectNotice.ConnectTimedOutMessage.Key));
            Assert.That(client.IsActive, Is.False);
        }

        [Test]
        public void UnresolvableHostEndsAsAFailedConnection()
        {
            var client = Track(new UdpMatchTransport());

            client.Connect("::1", 7777);

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected);
            Assert.That(clientEvent.Reason, Is.Empty);
            Assert.That(client.IsActive, Is.False);
        }

        [Test]
        public void ClientWithAnotherKeyIsRejectedAsProtocolMismatch()
        {
            var server = Track(new UdpMatchTransport());
            server.Listen(_loopback,0);
            var client = Track(new UdpMatchTransport(new UdpTransportOptions { ConnectionKey = "other-game" }));

            client.Connect(_loopback,(ushort)server.LocalPort);

            var clientEvent = WaitFor(client, TransportEventKind.Disconnected, server);
            Assert.That(clientEvent.Reason, Is.EqualTo(UdpMatchTransport.ProtocolMismatchReason));
            Assert.That(DisconnectNotice.Classify(clientEvent.Reason, false, null).Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(server.TryReceive(out _), Is.False);
        }

        [Test]
        public void RoundTripTimeIsMeasuredOnceConnected()
        {
            var options = new UdpTransportOptions { PingIntervalMilliseconds = 20 };
            var (server, client, serverSide, clientSide) = ConnectPair(options);

            Assert.That(client.TryGetRoundTripTime(new ConnectionId(999), out _), Is.False);
            var clientRtt = -1;
            var serverRtt = -1;
            Pump(
                () => client.TryGetRoundTripTime(clientSide, out clientRtt) & server.TryGetRoundTripTime(serverSide, out serverRtt),
                server,
                client);

            Assert.That(clientRtt, Is.GreaterThanOrEqualTo(0));
            Assert.That(serverRtt, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ServerTellsConnectionsApart()
        {
            var server = Track(new UdpMatchTransport());
            server.Listen(_loopback,0);
            var first = Track(new UdpMatchTransport());
            var second = Track(new UdpMatchTransport());
            first.Connect(_loopback,(ushort)server.LocalPort);
            second.Connect(_loopback,(ushort)server.LocalPort);

            var connected = new List<ConnectionId>();
            Pump(
                () =>
                {
                    while (server.TryReceive(out var e))
                    {
                        if (e.Kind == TransportEventKind.Connected)
                        {
                            connected.Add(e.Connection);
                        }
                    }

                    return connected.Count == 2;
                },
                first,
                second);

            Assert.That(connected.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void ClientCanConnectAgainAfterItsConnectionEnds()
        {
            var (server, client, serverSide, _) = ConnectPair();
            server.Disconnect(serverSide, null);
            WaitFor(client, TransportEventKind.Disconnected, server);

            client.Connect(_loopback,(ushort)server.LocalPort);

            var again = WaitFor(client, TransportEventKind.Connected, server);
            Assert.That(again.Connection, Is.Not.EqualTo(serverSide));
        }

        [Test]
        public void SendRejectsEmptyOrOversizedPayloads()
        {
            var (_, client, _, clientSide) = ConnectPair();

            Assert.Throws<ArgumentException>(
                () => client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(Array.Empty<byte>())));
            Assert.Throws<ArgumentException>(
                () => client.Send(
                    clientSide,
                    TransportChannel.Unreliable,
                    new ArraySegment<byte>(new byte[UdpMatchTransport.MaxUnreliablePayloadSize + 1])));
            Assert.Throws<ArgumentException>(
                () => client.Send(
                    clientSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[UdpMatchTransport.MaxReliablePayloadSize + 1])));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => client.Send(clientSide, (TransportChannel)9, new ArraySegment<byte>(new byte[1])));
        }

        [Test]
        public void UnknownConnectionsAreIgnored()
        {
            var (_, client, _, _) = ConnectPair();

            Assert.DoesNotThrow(() => client.Send(new ConnectionId(999), TransportChannel.Reliable, new ArraySegment<byte>(new byte[1])));
            Assert.DoesNotThrow(() => client.Disconnect(new ConnectionId(999), "x"));
        }

        [Test]
        public void TooLongReasonIsRejected()
        {
            var (server, _, serverSide, _) = ConnectPair();
            var tooLong = new string('a', TransportEvent.MaxReasonBytes + 1);

            Assert.Throws<ArgumentException>(() => server.Disconnect(serverSide, tooLong));
            Assert.Throws<ArgumentException>(() => server.Shutdown(tooLong));
            Assert.That(server.IsActive, Is.True);
        }

        [Test]
        public void ListenAndConnectCheckTheirArguments()
        {
            var transport = Track(new UdpMatchTransport());

            Assert.Throws<ArgumentException>(() => transport.Listen("not-an-address", 0));
            Assert.Throws<ArgumentException>(() => transport.Listen("::1", 0));
            Assert.Throws<ArgumentException>(() => transport.Connect(" ", 7777));
            Assert.That(transport.IsActive, Is.False);
        }

        [Test]
        public void StartingTwiceThrows()
        {
            var transport = Track(new UdpMatchTransport());
            transport.Listen(_loopback,0);

            Assert.Throws<InvalidOperationException>(() => transport.Listen(_loopback,0));
            Assert.Throws<InvalidOperationException>(() => transport.Connect(_loopback,7777));
        }

        [Test]
        public void ListeningOnABusyPortThrows()
        {
            var first = Track(new UdpMatchTransport());
            first.Listen(_loopback,0);
            var second = Track(new UdpMatchTransport());

            Assert.Throws<InvalidOperationException>(() => second.Listen(_loopback,(ushort)first.LocalPort));
            Assert.That(second.IsActive, Is.False);
        }

        [Test]
        public void ShutdownTwiceIsHarmless()
        {
            var transport = Track(new UdpMatchTransport());
            transport.Listen(_loopback,0);

            transport.Shutdown(null);
            transport.Shutdown(null);

            Assert.That(transport.IsActive, Is.False);
            Assert.That(transport.LocalPort, Is.Zero);
        }

        private (UdpMatchTransport Server, UdpMatchTransport Client, ConnectionId ServerSide, ConnectionId ClientSide) ConnectPair(
            UdpTransportOptions options = null)
        {
            var server = Track(new UdpMatchTransport(options ?? new UdpTransportOptions()));
            server.Listen(_loopback,0);
            var client = Track(new UdpMatchTransport(options ?? new UdpTransportOptions()));
            client.Connect(_loopback,(ushort)server.LocalPort);

            var serverSide = ConnectionId.None;
            var clientSide = ConnectionId.None;
            Pump(
                () =>
                {
                    serverSide = NextConnected(server, serverSide);
                    clientSide = NextConnected(client, clientSide);
                    return serverSide.IsValid && clientSide.IsValid;
                });
            return (server, client, serverSide, clientSide);
        }

        private static ConnectionId NextConnected(UdpMatchTransport transport, ConnectionId found)
        {
            while (!found.IsValid && transport.TryReceive(out var e))
            {
                if (e.Kind == TransportEventKind.Connected)
                {
                    found = e.Connection;
                }
            }

            return found;
        }

        private UdpMatchTransport Track(UdpMatchTransport transport)
        {
            _transports.Add(transport);
            return transport;
        }

        /// <summary>
        /// 他の出来事を読み捨てながら kind が出てくるまで待つ。サーバーは TryReceive の中で接続の申し込みを受けるので、others も読み続ける。
        /// 読み続けた others の出来事は捨てるので、後で確かめたい出来事がある側は others に入れない。
        /// </summary>
        private static TransportEvent WaitFor(UdpMatchTransport transport, TransportEventKind kind, params IMatchTransport[] others)
        {
            TransportEvent found = default;
            var seen = false;
            Pump(
                () =>
                {
                    while (!seen && transport.TryReceive(out var e))
                    {
                        if (e.Kind == kind)
                        {
                            found = e;
                            seen = true;
                        }
                    }

                    return seen;
                },
                others);
            return found;
        }

        private static void Pump(Func<bool> done, params IMatchTransport[] others)
        {
            var watch = Stopwatch.StartNew();
            while (!done())
            {
                foreach (var other in others)
                {
                    while (other.TryReceive(out _))
                    {
                    }
                }

                if (watch.Elapsed > _timeout)
                {
                    Assert.Fail("timed out after " + _timeout.TotalSeconds + "s");
                }

                Thread.Sleep(5);
            }
        }

        private static void Pump(Func<bool> done, Collector collector, params IMatchTransport[] others)
        {
            Pump(
                () =>
                {
                    collector.Drain();
                    return done();
                },
                others);
        }

        /// <summary>届いたデータは次の TryReceive までしか保証されないので、その場で写して溜める。</summary>
        private sealed class Collector
        {
            private readonly UdpMatchTransport _transport;
            private readonly List<byte[]> _received;
            private readonly ConnectionId _from;
            private readonly TransportChannel _channel;

            public Collector(UdpMatchTransport transport, List<byte[]> received, ConnectionId from, TransportChannel channel)
            {
                _transport = transport;
                _received = received;
                _from = from;
                _channel = channel;
            }

            public void Drain()
            {
                while (_transport.TryReceive(out var e))
                {
                    if (e.Kind == TransportEventKind.Data)
                    {
                        Assert.That(e.Connection, Is.EqualTo(_from));
                        Assert.That(e.Channel, Is.EqualTo(_channel));
                        _received.Add(e.Payload.ToArray());
                    }
                }
            }
        }
    }
}
