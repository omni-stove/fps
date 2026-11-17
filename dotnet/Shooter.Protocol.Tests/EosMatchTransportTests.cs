using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class EosMatchTransportTests
    {
        private const string _serverId = "0002aaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _clientId = "0002bbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _otherClientId = "0002cccccccccccccccccccccccccccc";

        private FakeEosNetwork _network;
        private List<string> _log;
        private List<EosMatchTransport> _transports;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeEosNetwork();
            _log = new List<string>();
            _transports = new List<EosMatchTransport>();
            _unclaimed.Clear();
            _serverP2P = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var transport in _transports)
            {
                transport.Dispose();
            }
        }

        [Test]
        public void ServerSaysItIsReadyWithItsProductUserId()
        {
            var server = Server(out _);

            Assert.That(server.IsActive, Is.True);
            Assert.That(server.LocalUserId, Is.EqualTo(_serverId));
            Assert.That(_log, Has.Some.StartsWith("[Net] EOS server ready id=" + _serverId + " "));
            Assert.That(_log, Has.Some.Contains("-transport eos -eos-server " + _serverId));
        }

        [Test]
        public void ClientAndServerSeeEachOtherConnectAndTheServerKnowsWhoConnected()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            Assert.That(serverSide.IsValid, Is.True);
            Assert.That(clientSide.IsValid, Is.True);
            Assert.That(server.TryGetProductUserId(serverSide, out var userId), Is.True);
            Assert.That(userId, Is.EqualTo(_clientId));
            Assert.That(server.TryGetProductUserId(new ConnectionId(999), out _), Is.False);
            Assert.That(client.IsActive, Is.True);
        }

        [Test]
        public void ConnectAcceptsAnUpperCaseServerId()
        {
            var server = Server(out _);
            var client = Client(_clientId);

            client.Connect(_serverId.ToUpperInvariant(), 0);

            Assert.That(Next(server).Kind, Is.EqualTo(TransportEventKind.Connected));
            Assert.That(Next(client).Kind, Is.EqualTo(TransportEventKind.Connected));
        }

        [Test]
        public void ReliableMessagesArriveInOrder()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            for (byte i = 1; i <= 50; i++)
            {
                client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(new[] { i, (byte)(i * 2) }));
            }

            for (var i = 1; i <= 50; i++)
            {
                var received = Next(server);
                Assert.That(received.Kind, Is.EqualTo(TransportEventKind.Data));
                Assert.That(received.Connection, Is.EqualTo(serverSide));
                Assert.That(received.Channel, Is.EqualTo(TransportChannel.Reliable));
                Assert.That(received.Payload, Is.EqualTo(new[] { (byte)i, (byte)(i * 2) }));
            }
        }

        [Test]
        public void ReliableMessageLargerThanOnePacketArrivesWhole()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var payload = new byte[EosMatchTransport.MaxReliablePayloadSize];
            new Random(7).NextBytes(payload);

            server.Send(serverSide, TransportChannel.Reliable, new ArraySegment<byte>(payload));

            var received = Next(client);
            Assert.That(received.Kind, Is.EqualTo(TransportEventKind.Data));
            Assert.That(received.Payload, Is.EqualTo(payload));
        }

        [Test]
        public void UnreliableMessageOfTheLargestSizeArrivesOnTheUnreliableChannel()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var payload = new byte[EosMatchTransport.MaxUnreliablePayloadSize];
            payload[0] = 42;
            payload[payload.Length - 1] = 7;

            client.Send(clientSide, TransportChannel.Unreliable, new ArraySegment<byte>(payload));

            var received = Next(server);
            Assert.That(received.Channel, Is.EqualTo(TransportChannel.Unreliable));
            Assert.That(received.Payload, Is.EqualTo(payload));
        }

        [Test]
        public void PayloadIsCopiedWhenSent()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var payload = new byte[] { 1, 2, 3 };

            client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(payload));
            payload[0] = 9;

            Assert.That(Next(server).Payload, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void SendRejectsEmptyOrOversizedPayloads()
        {
            var (_, client, _, clientSide) = ConnectPair();

            Assert.That(
                () => client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(Array.Empty<byte>())),
                Throws.ArgumentException);
            Assert.That(
                () => client.Send(clientSide, TransportChannel.Unreliable, new ArraySegment<byte>(new byte[EosMatchTransport.MaxUnreliablePayloadSize + 1])),
                Throws.ArgumentException);
            Assert.That(
                () => client.Send(clientSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[EosMatchTransport.MaxReliablePayloadSize + 1])),
                Throws.ArgumentException);
            Assert.That(client.GetMaxPayloadSize(TransportChannel.Unreliable), Is.EqualTo(EosMatchTransport.MaxUnreliablePayloadSize));
        }

        [Test]
        public void ServerDisconnectDeliversItsDataThenItsReason()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();
            var reason = DisconnectNotice.RejectedReason("version-mismatch");

            server.Send(serverSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 5 }));
            server.Disconnect(serverSide, reason);
            server.Send(serverSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 6 }));

            Assert.That(Next(client).Payload, Is.EqualTo(new byte[] { 5 }));
            var ended = Next(client);
            Assert.That(ended.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(ended.Connection, Is.EqualTo(clientSide));
            Assert.That(ended.Reason, Is.EqualTo(reason));

            var serverEnded = Next(server);
            Assert.That(serverEnded.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(serverEnded.Connection, Is.EqualTo(serverSide));
            Assert.That(serverEnded.Reason, Is.EqualTo(reason));
            Assert.That(client.IsActive, Is.False);
        }

        [Test]
        public void ClientLeavingTellsTheServerItsReason()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            client.Disconnect(clientSide, "client-left");

            var ended = Next(server);
            Assert.That(ended.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(ended.Connection, Is.EqualTo(serverSide));
            Assert.That(ended.Reason, Is.EqualTo("client-left"));
            var own = Next(client);
            Assert.That(own.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(own.Reason, Is.EqualTo("client-left"));
        }

        [Test]
        public void ServerShutdownReachesTheClientAsServerStopped()
        {
            var (server, client, _, clientSide) = ConnectPair();

            server.Shutdown(DisconnectNotice.ServerStoppedReason);

            Assert.That(server.IsActive, Is.False);
            var ended = Next(client);
            Assert.That(ended.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(ended.Connection, Is.EqualTo(clientSide));
            Assert.That(ended.Reason, Is.EqualTo(DisconnectNotice.ServerStoppedReason));
            Assert.That(server.TryReceive(out _), Is.False);
        }

        [Test]
        public void PeerThatVanishesEndsWithoutAReason()
        {
            var (server, client, serverSide, clientSide) = ConnectPair();

            _network.Sever(_serverId, _clientId);

            var serverEnded = Next(server);
            Assert.That(serverEnded.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(serverEnded.Connection, Is.EqualTo(serverSide));
            Assert.That(serverEnded.Reason, Is.Empty);
            var clientEnded = Next(client);
            Assert.That(clientEnded.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(clientEnded.Connection, Is.EqualTo(clientSide));
            Assert.That(clientEnded.Reason, Is.Empty);
        }

        [Test]
        public void PlayerWhoReconnectsInTheSameTickGetsANewConnection()
        {
            var (server, _, serverSide, _) = ConnectPair();
            _network.Sever(_serverId, _clientId);
            var again = Client(_clientId);

            again.Connect(_serverId, 0);
            Assert.That(again.TryReceive(out _), Is.False);

            var left = Next(server);
            Assert.That(left.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(left.Connection, Is.EqualTo(serverSide));
            var joined = Next(server);
            Assert.That(joined.Kind, Is.EqualTo(TransportEventKind.Connected));
            Assert.That(joined.Connection, Is.Not.EqualTo(serverSide));
            Assert.That(server.TryGetProductUserId(joined.Connection, out var userId), Is.True);
            Assert.That(userId, Is.EqualTo(_clientId));
        }

        [Test]
        public void ServerClosesAPeerThatNeverAnswersItsDisconnect()
        {
            var (server, _, serverSide, _) = ConnectPair();
            _network.DropAll = true;

            server.Disconnect(serverSide, "bye");
            Assert.That(server.TryReceive(out _), Is.False);
            _network.Now += EosTransportOptions.DefaultCloseLingerMilliseconds;

            var ended = Next(server);
            Assert.That(ended.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(ended.Reason, Is.EqualTo("bye"));
            Assert.That(_serverP2P.Closed, Does.Contain(_clientId));
        }

        [Test]
        public void ConnectingToNobodyTimesOutAndTheClientCanConnectAgain()
        {
            var client = Client(_clientId);

            client.Connect(_serverId, 0);
            Assert.That(client.TryReceive(out _), Is.False);
            _network.Now += EosTransportOptions.DefaultConnectTimeoutMilliseconds;

            var ended = Next(client);
            Assert.That(ended.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(ended.Reason, Is.Empty);
            Assert.That(client.IsActive, Is.False);
            Assert.That(_log, Has.Some.Contains("timed out"));

            var server = Server(out _);
            client.Connect(_serverId, 0);
            Assert.That(Next(client).Kind, Is.EqualTo(TransportEventKind.Connected));
            Assert.That(Next(server).Kind, Is.EqualTo(TransportEventKind.Connected));
        }

        [Test]
        public void ConnectingToItsOwnIdEndsTheConnection()
        {
            var client = Client(_clientId);

            client.Connect(_clientId, 0);

            Assert.That(Next(client).Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(_log, Has.Some.Contains("own id"));
        }

        [Test]
        public void ClientThatCannotSignInEndsTheConnection()
        {
            var client = new EosMatchTransport(Join(_clientId, p => p.SignInProblem = "EOS connect login failed: InvalidAuth"), new EosTransportOptions(), _log.Add, () => _network.Now);
            _transports.Add(client);

            client.Connect(_serverId, 0);

            Assert.That(Next(client).Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(_log, Has.Some.Contains("InvalidAuth"));
        }

        [Test]
        public void ClientThatCannotStartEndsTheConnection()
        {
            var client = new EosMatchTransport(Join(_clientId, p => p.BeginProblem = "no SDK"), new EosTransportOptions(), _log.Add, () => _network.Now);
            _transports.Add(client);

            client.Connect(_serverId, 0);

            Assert.That(Next(client).Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(client.IsActive, Is.False);
        }

        [Test]
        public void ListenThrowsWhenTheServerCannotStartOrSignIn()
        {
            var cannotStart = new EosMatchTransport(Join(_serverId, p => p.BeginProblem = "no SDK"), new EosTransportOptions(), _log.Add, () => _network.Now);
            Assert.That(() => cannotStart.Listen("0.0.0.0", 0), Throws.InvalidOperationException.With.Message.EqualTo("no SDK"));
            Assert.That(cannotStart.IsActive, Is.False);

            var cannotSignIn = new EosMatchTransport(Join(_serverId, p => p.SignInProblem = "login failed"), new EosTransportOptions(), _log.Add, () => _network.Now);
            Assert.That(() => cannotSignIn.Listen("0.0.0.0", 0), Throws.InvalidOperationException.With.Message.EqualTo("login failed"));
            Assert.That(cannotSignIn.IsActive, Is.False);
        }

        [Test]
        public void ListenGivesUpWhenTheLoginNeverFinishes()
        {
            var p2p = Join(_serverId, p => p.SignsIn = false);
            var options = new EosTransportOptions { SignInTimeoutMilliseconds = 50 };
            var server = new EosMatchTransport(p2p, options, _log.Add, () => _network.Now += 10);

            Assert.That(() => server.Listen("0.0.0.0", 0), Throws.InvalidOperationException.With.Message.Contains("did not finish"));
            Assert.That(p2p.CloseAllCount, Is.EqualTo(1));
        }

        [Test]
        public void ConnectChecksTheServerIdAndListenOrConnectOnlyOnce()
        {
            var client = Client(_clientId);
            Assert.That(() => client.Connect("127.0.0.1", 7777), Throws.ArgumentException);
            Assert.That(() => client.Connect(" ", 7777), Throws.ArgumentException);
            Assert.That(client.IsActive, Is.False);

            var server = Server(out _);
            Assert.That(() => server.Listen("0.0.0.0", 0), Throws.InvalidOperationException);
            Assert.That(() => server.Connect(_clientId, 0), Throws.InvalidOperationException);
        }

        [Test]
        public void ServerTellsConnectionsApart()
        {
            var server = Server(out _);
            var first = Client(_clientId);
            var second = Client(_otherClientId);
            first.Connect(_serverId, 0);
            second.Connect(_serverId, 0);

            var a = Next(server);
            var b = Next(server);
            Assert.That(a.Connection, Is.Not.EqualTo(b.Connection));
            var firstSide = Next(first).Connection;
            var secondSide = Next(second).Connection;

            first.Send(firstSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 1 }));
            second.Send(secondSide, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 2 }));

            var fromFirst = Next(server);
            var fromSecond = Next(server);
            Assert.That(server.TryGetProductUserId(fromFirst.Connection, out var firstUser), Is.True);
            Assert.That(firstUser, Is.EqualTo(_clientId));
            Assert.That(fromFirst.Payload, Is.EqualTo(new byte[] { 1 }));
            Assert.That(server.TryGetProductUserId(fromSecond.Connection, out var secondUser), Is.True);
            Assert.That(secondUser, Is.EqualTo(_otherClientId));
        }

        [Test]
        public void RoundTripTimeIsMeasuredOnceConnected()
        {
            _network.DropUnreliable = true;
            var (server, client, _, clientSide) = ConnectPair();
            Assert.That(client.TryGetRoundTripTime(clientSide, out _), Is.False);
            _network.DropUnreliable = false;

            _network.Now += EosTransportOptions.DefaultPingIntervalMilliseconds;
            Assert.That(client.TryReceive(out _), Is.False);
            _network.Now += 40;
            Assert.That(server.TryReceive(out _), Is.False);
            Assert.That(client.TryReceive(out _), Is.False);

            Assert.That(client.TryGetRoundTripTime(clientSide, out var milliseconds), Is.True);
            Assert.That(milliseconds, Is.EqualTo(40));
            Assert.That(client.TryGetRoundTripTime(new ConnectionId(999), out _), Is.False);
        }

        [Test]
        public void PacketsFromStrangersAndMalformedPacketsAreDropped()
        {
            var (server, _, _, _) = ConnectPair();

            _serverP2P.Inject(_otherClientId, EosMatchTransport.ReliableChannel, new byte[] { PacketSplitter.WholeTag, 0, 1 });
            _serverP2P.Inject(_clientId, EosMatchTransport.UnreliableChannel, new byte[] { PacketSplitter.FragmentTag, 0, 1 });
            _serverP2P.Inject(_clientId, EosMatchTransport.ReliableChannel, new byte[] { PacketSplitter.WholeTag, 77, 1 });
            _serverP2P.Inject(_clientId, 3, new byte[] { PacketSplitter.WholeTag, 0, 1 });

            Assert.That(server.TryReceive(out _), Is.False);
            Assert.That(_log, Has.Some.Contains("malformed"));
        }

        [Test]
        public void TooLongReasonIsRejected()
        {
            var (server, _, serverSide, _) = ConnectPair();
            var reason = new string('x', TransportEvent.MaxReasonBytes + 1);

            Assert.That(() => server.Disconnect(serverSide, reason), Throws.ArgumentException);
            Assert.That(() => server.Shutdown(reason), Throws.ArgumentException);
        }

        private FakeEosP2P _serverP2P;

        private FakeEosP2P Join(string userId, Action<FakeEosP2P> configure)
        {
            var p2p = _network.Join(userId);
            configure(p2p);
            return p2p;
        }

        private EosMatchTransport Server(out FakeEosP2P p2p)
        {
            p2p = _network.Join(_serverId);
            _serverP2P = p2p;
            var server = new EosMatchTransport(p2p, new EosTransportOptions(), _log.Add, () => _network.Now);
            _transports.Add(server);
            server.Listen("0.0.0.0", 0);
            return server;
        }

        private EosMatchTransport Client(string userId)
        {
            var client = new EosMatchTransport(_network.Join(userId), new EosTransportOptions(), _log.Add, () => _network.Now);
            _transports.Add(client);
            return client;
        }

        private (EosMatchTransport Server, EosMatchTransport Client, ConnectionId ServerSide, ConnectionId ClientSide) ConnectPair()
        {
            var server = Server(out _);
            var client = Client(_clientId);
            client.Connect(_serverId, 0);

            var serverSide = Next(server);
            Assert.That(serverSide.Kind, Is.EqualTo(TransportEventKind.Connected));
            var clientSide = Next(client);
            Assert.That(clientSide.Kind, Is.EqualTo(TransportEventKind.Connected));
            return (server, client, serverSide.Connection, clientSide.Connection);
        }

        // 相手の側を進めないと届かないものもあるので、取れるまで両方を数回回す
        private Received Next(EosMatchTransport transport)
        {
            for (var i = 0; i < 20; i++)
            {
                for (var j = 0; j < _unclaimed.Count; j++)
                {
                    if (_unclaimed[j].Owner == transport)
                    {
                        var claimed = _unclaimed[j].Event;
                        _unclaimed.RemoveAt(j);
                        return claimed;
                    }
                }

                if (transport.TryReceive(out var transportEvent))
                {
                    return new Received(transportEvent);
                }

                foreach (var other in _transports)
                {
                    if (other != transport && other.IsActive && other.TryReceive(out var otherEvent))
                    {
                        _unclaimed.Add((other, new Received(otherEvent)));
                    }
                }
            }

            Assert.Fail("no transport event arrived");
            return null;
        }

        private readonly List<(EosMatchTransport Owner, Received Event)> _unclaimed = new List<(EosMatchTransport Owner, Received Event)>();

        private sealed class Received
        {
            public Received(TransportEvent transportEvent)
            {
                Kind = transportEvent.Kind;
                Connection = transportEvent.Connection;
                Channel = transportEvent.Channel;
                Reason = transportEvent.Reason;
                Payload = transportEvent.Kind == TransportEventKind.Data ? transportEvent.Payload.AsSpan().ToArray() : null;
            }

            public TransportEventKind Kind { get; }

            public ConnectionId Connection { get; }

            public TransportChannel Channel { get; }

            public string Reason { get; }

            public byte[] Payload { get; }
        }
    }
}
