using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientAntiCheatTests
    {
        private readonly List<byte[]> _sent = new List<byte[]>();
        private readonly List<string> _log = new List<string>();
        private readonly List<string> _removed = new List<string>();
        private Session _session;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _log.Clear();
            _removed.Clear();
            _session = new Session();
        }

        [Test]
        public void ADisabledClientSaysHelloAndPlaysWithADisabledServer()
        {
            var antiCheat = Create(false);

            Assert.That(antiCheat.ServerConnected(0, out var error), Is.True, error);
            antiCheat.HandleMessage(Hello(false), 0.1);

            Assert.That(Read(_sent.Single()).Kind, Is.EqualTo(AntiCheatPacketKind.Hello));
            Assert.That(Read(_sent.Single()).PeerEnabled, Is.False);
            Assert.That(_session.Began, Is.False);
            Assert.That(_log, Has.Some.Contains("server id=0 also runs without anti-cheat"));
            Assert.That(_removed, Is.Empty);
        }

        [Test]
        public void AnEnabledClientStartsTheSessionInTheOrderTheServerExpects()
        {
            var antiCheat = Create(true);

            Assert.That(antiCheat.ServerConnected(0, out _), Is.True);
            _session.Emit(4, 5);

            Assert.That(_session.Began, Is.True);
            Assert.That(Read(_sent[0]).Kind, Is.EqualTo(AntiCheatPacketKind.Hello));
            Assert.That(Read(_sent[0]).PeerEnabled, Is.True);
            Assert.That(Read(_sent[1]).Payload.ToArray(), Is.EqualTo(new byte[] { 4, 5 }));
            Assert.That(_log, Has.Some.EqualTo("[AntiCheat] session started (client-server)"));
        }

        [Test]
        public void ServerMessagesAreKeptUntilItsHelloAndThenPassedOnInOrder()
        {
            var antiCheat = Create(true);
            antiCheat.ServerConnected(0, out _);

            antiCheat.HandleMessage(Message(1), 0.1);
            antiCheat.HandleMessage(Message(2), 0.2);
            antiCheat.HandleMessage(Hello(true), 0.3);
            antiCheat.HandleMessage(Message(3), 0.4);

            Assert.That(_session.Received, Is.EqualTo(new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 } }));
            Assert.That(_log, Has.Some.Contains("server id=0 runs anti-cheat"));
        }

        [Test]
        public void AFailedSessionIsReported()
        {
            _session.BeginError = "anti-cheat BeginSession failed: AntiCheatClientProtectionNotAvailable";
            var antiCheat = Create(true);

            Assert.That(antiCheat.ServerConnected(0, out var error), Is.False);
            Assert.That(error, Is.EqualTo(_session.BeginError));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void AMismatchLeavesAfterTheHelloHadTimeToReachTheServer()
        {
            var antiCheat = Create(false);
            antiCheat.ServerConnected(0, out _);

            antiCheat.HandleMessage(Hello(true), 0.1);
            antiCheat.Tick(0.5);

            Assert.That(_log, Has.Some.EqualTo("[AntiCheat] removing peer id=0: the peer requires anti-cheat but this side runs without it"));
            Assert.That(_removed, Is.Empty);

            antiCheat.Tick(1.2);

            Assert.That(_removed.Single(), Is.EqualTo("the peer requires anti-cheat but this side runs without it"));
            Assert.That(antiCheat.Removed, Is.True);
        }

        [Test]
        public void AnEnabledClientLeavesAServerWithoutAntiCheat()
        {
            var antiCheat = Create(true);
            antiCheat.ServerConnected(0, out _);

            antiCheat.HandleMessage(Hello(false), 0.1);
            antiCheat.Tick(2);

            Assert.That(_removed.Single(), Is.EqualTo("the peer runs without anti-cheat (-noeac or an unprotected build)"));
            Assert.That(_session.Ended, Is.True);
        }

        [Test]
        public void EndingBeforeTheDelayedLeaveCancelsIt()
        {
            var antiCheat = Create(false);
            antiCheat.ServerConnected(0, out _);
            antiCheat.HandleMessage(Hello(true), 0.1);

            antiCheat.End();
            antiCheat.Tick(5);

            Assert.That(_removed, Is.Empty);
        }

        [Test]
        public void HelloIsResentUntilTheServerAnswersAndThenTimesOut()
        {
            var antiCheat = Create(false);
            antiCheat.ServerConnected(0, out _);

            for (var t = 1; t <= 14; t++)
            {
                antiCheat.Tick(t);
            }

            Assert.That(_sent, Has.Count.EqualTo(15));
            Assert.That(_removed, Is.Empty);

            antiCheat.Tick(15);

            Assert.That(_removed.Single(), Does.StartWith("no anti-cheat hello within 15s"));
        }

        [Test]
        public void AnIntegrityViolationLeaves()
        {
            var antiCheat = Create(true);
            antiCheat.ServerConnected(0, out _);

            _session.Violate("local integrity violation IntegrityCatalogNotFound: missing");

            Assert.That(_removed.Single(), Is.EqualTo("anti-cheat: local integrity violation IntegrityCatalogNotFound: missing"));
            Assert.That(_session.Ended, Is.True);
        }

        [Test]
        public void MalformedPacketsAreDropped()
        {
            var antiCheat = Create(true);
            antiCheat.ServerConnected(0, out _);
            var message = new AntiCheatToClientMessage();
            message.SetPacket(new ArraySegment<byte>(new byte[] { 9 }));

            antiCheat.HandleMessage(message, 0.1);

            Assert.That(_removed, Is.Empty);
            Assert.That(_log, Has.Some.Contains("dropped a malformed packet from id=0"));
        }

        [Test]
        public void AnEnabledClientNeedsASession()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientAntiCheat(true, null, _ => { }, _ => { }, _ => { }));
        }

        private static AntiCheatPacket Read(byte[] packet)
        {
            return AntiCheatPacket.Read(new ArraySegment<byte>(packet), out _);
        }

        private static AntiCheatToClientMessage Hello(bool enabled)
        {
            var buffer = new byte[AntiCheatPacket.HelloSize];
            AntiCheatPacket.WriteHello(buffer, enabled);
            var message = new AntiCheatToClientMessage();
            message.SetPacket(new ArraySegment<byte>(buffer));
            return message;
        }

        private static AntiCheatToClientMessage Message(params byte[] payload)
        {
            var buffer = new byte[AntiCheatPacket.MaxPacketSize];
            var length = AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(payload));
            var message = new AntiCheatToClientMessage();
            message.SetPacket(new ArraySegment<byte>(buffer, 0, length));
            return message;
        }

        private ClientAntiCheat Create(bool enabled)
        {
            return new ClientAntiCheat(
                enabled,
                enabled ? _session : null,
                message => _sent.Add(((AntiCheatToServerMessage)message).Packet.ToArray()),
                _log.Add,
                _removed.Add);
        }

        private sealed class Session : IAntiCheatClientSession
        {
            private Action<ArraySegment<byte>> _send;
            private Action<string> _fail;

            public string BeginError { get; set; }

            public bool Began { get; private set; }

            public bool Ended { get; private set; }

            public List<byte[]> Received { get; } = new List<byte[]>();

            public bool BeginSession(Action<ArraySegment<byte>> sendToServer, Action<string> fail, out string reason)
            {
                _send = sendToServer;
                _fail = fail;
                reason = BeginError;
                Began = reason == null;
                return Began;
            }

            public bool ReceiveMessageFromServer(ArraySegment<byte> message, out string reason)
            {
                Received.Add(message.ToArray());
                reason = null;
                return true;
            }

            public void EndSession()
            {
                Ended = true;
            }

            public void Emit(params byte[] message)
            {
                _send(new ArraySegment<byte>(message));
            }

            public void Violate(string reason)
            {
                _fail(reason);
            }
        }
    }
}
