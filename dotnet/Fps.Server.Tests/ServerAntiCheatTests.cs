using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Protocol;
using Fps.Server.Eos;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerAntiCheatTests
    {
        private const string _user = "0123456789abcdef0123456789abcdef";
        private static readonly ConnectionId _alice = new ConnectionId(3);

        private StringWriter _log;
        private RecordingTransport _transport;
        private StubControl _control;
        private FakeBackend _backend;
        private FakeUserIds _userIds;
        private ServerAntiCheat _module;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
            _backend = new FakeBackend();
            _userIds = new FakeUserIds();
            _userIds.Ids[_alice] = _user;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Dispose();
            _backend.Dispose();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void DisabledServerSaysHelloAndAcceptsAClientWithoutAntiCheat()
        {
            Start(false);

            _module.OnConnected(_alice);
            Hello(false);

            Assert.That(Packets().Single().Kind, Is.EqualTo(AntiCheatPacketKind.Hello));
            Assert.That(Packets().Single().PeerEnabled, Is.False);
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[AntiCheat] session not started (disabled)"));
            Assert.That(_log.ToString(), Does.Contain("peer id=3 also runs without anti-cheat; continuing unprotected"));
        }

        [Test]
        public void DisabledServerRemovesAClientThatRequiresAntiCheat()
        {
            Start(false);
            _module.OnConnected(_alice);

            Hello(true);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, "fps:rejected:anti-cheat") }));
            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] removing peer id=3: the peer requires anti-cheat but this side runs without it"));
        }

        [Test]
        public void EnabledServerRemovesAClientWithoutAntiCheat()
        {
            Start(true);
            _module.OnConnected(_alice);

            Hello(false);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (_alice, "fps:rejected:anti-cheat") }));
            Assert.That(_log.ToString(), Does.Contain(
                "[AntiCheat] removing peer id=3: the peer runs without anti-cheat (-noeac or an unprotected build)"));
            Assert.That(_backend.Registered, Is.Empty);
        }

        [Test]
        public void EnabledServerRegistersAProtectedClientByItsProductUserIdAndRelaysBothWays()
        {
            Start(true);
            _module.OnConnected(_alice);

            Hello(true);
            Message(1, 2, 3);
            _backend.RaiseMessageToClient(_backend.Registered.Single().Handle, 9, 8);

            Assert.That(_backend.Began, Is.True);
            Assert.That(_backend.Registered.Single().UserId, Is.EqualTo(_user));
            Assert.That(_backend.Received.Single(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            var relayed = Packets().Last();
            Assert.That(relayed.Kind, Is.EqualTo(AntiCheatPacketKind.Message));
            Assert.That(relayed.Payload.ToArray(), Is.EqualTo(new byte[] { 9, 8 }));
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[AntiCheat] registered peer id=3 user=" + _user + " handle=1"));
        }

        [Test]
        public void MessagesBeforeTheHelloAreKeptAndPassedOnInOrder()
        {
            Start(true);
            _module.OnConnected(_alice);

            Message(1);
            Message(2);
            Hello(true);

            Assert.That(_backend.Received, Is.EqualTo(new[] { new byte[] { 1 }, new byte[] { 2 } }));
        }

        [Test]
        public void AClientWithoutAKnownProductUserIdIsRemoved()
        {
            _userIds.Ids.Clear();
            Start(true);
            _module.OnConnected(_alice);

            Hello(true);

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("removing peer id=3: its EOS product user id is unknown"));
        }

        [Test]
        public void AFailedRegistrationRemovesTheClient()
        {
            _backend.RegisterError = "RegisterClient failed: InvalidParameters";
            Start(true);
            _module.OnConnected(_alice);

            Hello(true);

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("removing peer id=3: RegisterClient failed: InvalidParameters"));
        }

        [Test]
        public void ARemovePlayerActionKicksAndUnregisters()
        {
            Start(true);
            _module.OnConnected(_alice);
            Hello(true);
            var handle = _backend.Registered.Single().Handle;

            _backend.RaiseAction(handle, true, "RemovePlayer InternalError");

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_backend.Unregistered, Is.EqualTo(new[] { handle }));
            Assert.That(_log.ToString(), Does.Contain("removing peer id=3: anti-cheat violation RemovePlayer InternalError"));
        }

        [Test]
        public void OtherActionsAreOnlyLogged()
        {
            Start(true);
            _module.OnConnected(_alice);
            Hello(true);

            _backend.RaiseAction(_backend.Registered.Single().Handle, false, "Invalid InternalError");

            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("ignoring action Invalid InternalError for id=3"));
        }

        [Test]
        public void DisconnectingUnregistersTheClient()
        {
            Start(true);
            _module.OnConnected(_alice);
            Hello(true);

            _module.OnDisconnected(_alice);

            Assert.That(_backend.Unregistered, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[AntiCheat] unregistered peer id=3"));
        }

        [Test]
        public void HelloIsResentEverySecondAndAMissingHelloRemovesTheClient()
        {
            Start(false);
            _module.OnConnected(_alice);

            for (var i = 0; i < 14 * 4; i++)
            {
                _module.Tick(i, 0.25f);
            }

            Assert.That(Packets(), Has.Count.EqualTo(15));
            Assert.That(_control.Kicks, Is.Empty);

            _module.Tick(100, 1f);

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("removing peer id=3: no anti-cheat hello within 15s"));
        }

        [Test]
        public void MalformedPacketsAreDroppedAndLoggedOnce()
        {
            Start(true);
            _module.OnConnected(_alice);

            Raw(7);
            Raw(7);

            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(CountOf(_log.ToString(), "dropped a malformed packet from id=3"), Is.EqualTo(1));
        }

        [Test]
        public void OtherMessagesAreIgnored()
        {
            Start(true);
            _module.OnConnected(_alice);
            var before = _transport.Sent.Count;

            _module.OnMessage(_alice, new PingRequestMessage());

            Assert.That(_transport.Sent, Has.Count.EqualTo(before));
        }

        [Test]
        public void AnEnabledServerNeedsATransportThatKnowsProductUserIds()
        {
            _userIds = null;

            var error = Assert.Throws<InvalidOperationException>(() => Start(true));

            Assert.That(error.Message, Does.Contain("-transport eos"));
            Assert.That(_backend.Began, Is.False);
        }

        [Test]
        public void AFailedSessionStopsTheServerFromStarting()
        {
            _backend.BeginError = "anti-cheat BeginSession failed: AntiCheatInvalidMode";

            var error = Assert.Throws<InvalidOperationException>(() => Start(true));

            Assert.That(error.Message, Is.EqualTo("[AntiCheat] anti-cheat BeginSession failed: AntiCheatInvalidMode"));
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void StopEndsTheSession()
        {
            Start(true);

            _module.Stop();

            Assert.That(_backend.Ended, Is.True);
            Assert.That(_backend.Disposed, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[AntiCheat] session ended"));
        }

        [Test]
        public void TickPumpsTheBackend()
        {
            Start(true);

            _module.Tick(1, 0.1f);

            Assert.That(_backend.Ticks, Is.EqualTo(1));
        }

        private static int CountOf(string text, string part)
        {
            return (text.Length - text.Replace(part, "", StringComparison.Ordinal).Length) / part.Length;
        }

        private void Start(bool enabled)
        {
            var settings = enabled
                ? Settings("-eac", NetTransportKind.Eos)
                : Settings(null, NetTransportKind.Udp);
            _module = new ServerAntiCheat(settings, enabled ? _ => _backend : null);
            var match = new ServerMatch(0f, _log, new ServerEventWriter(false, _log, _log), () => DateTime.UtcNow);
            _module.Start(new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "0.3.0",
                _log,
                TestMaps.Data(),
                match,
                new ServerMessenger(_transport),
                _control,
                userIds: _userIds));
        }

        private static AntiCheatSettings Settings(string flag, NetTransportKind transport)
        {
            var args = flag == null ? Array.Empty<string>() : new[] { flag };
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, transport, null);
            Assert.That(AntiCheatSettings.TryResolveDedicatedServer(args, options, out var settings, out var error), Is.True, error);
            return settings;
        }

        private void Hello(bool enabled)
        {
            var buffer = new byte[AntiCheatPacket.HelloSize];
            AntiCheatPacket.WriteHello(buffer, enabled);
            Send(buffer);
        }

        private void Message(params byte[] payload)
        {
            var buffer = new byte[AntiCheatPacket.MaxPacketSize];
            var length = AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(payload));
            Send(buffer.Take(length).ToArray());
        }

        private void Raw(params byte[] packet)
        {
            Send(packet);
        }

        private void Send(byte[] packet)
        {
            var message = new AntiCheatToServerMessage();
            message.SetPacket(new ArraySegment<byte>(packet));
            _module.OnMessage(_alice, message);
        }

        private List<AntiCheatPacket> Packets()
        {
            return _transport.Read<AntiCheatToClientMessage>(3, m =>
                {
                    var copy = new AntiCheatToClientMessage();
                    copy.SetPacket(new ArraySegment<byte>(m.Packet.ToArray()));
                    return copy;
                })
                .Select(m => AntiCheatPacket.Read(m.Packet, out _))
                .ToList();
        }

        private sealed class FakeUserIds : IConnectionUserIds
        {
            public Dictionary<ConnectionId, string> Ids { get; } = new Dictionary<ConnectionId, string>();

            public bool TryGetProductUserId(ConnectionId connection, out string productUserId)
            {
                return Ids.TryGetValue(connection, out productUserId);
            }
        }

        private sealed class FakeBackend : IAntiCheatServerBackend
        {
            public event Action<long, ArraySegment<byte>> MessageToClient;

            public event Action<long, bool, string> ClientActionRequired;

            public event Action<long, string> ClientAuthStatusChanged;

            public string BeginError { get; set; }

            public string RegisterError { get; set; }

            public bool Began { get; private set; }

            public bool Ended { get; private set; }

            public bool Disposed { get; private set; }

            public int Ticks { get; private set; }

            public List<(long Handle, string UserId)> Registered { get; } = new List<(long Handle, string UserId)>();

            public List<long> Unregistered { get; } = new List<long>();

            public List<byte[]> Received { get; } = new List<byte[]>();

            public bool Begin(out string error)
            {
                error = BeginError;
                Began = error == null;
                return Began;
            }

            public bool RegisterClient(long handle, string productUserId, out string error)
            {
                error = RegisterError;
                if (error == null)
                {
                    Registered.Add((handle, productUserId));
                }

                return error == null;
            }

            public bool UnregisterClient(long handle, out string error)
            {
                Unregistered.Add(handle);
                error = null;
                return true;
            }

            public bool ReceiveMessageFromClient(long handle, ArraySegment<byte> message, out string error)
            {
                Received.Add(message.ToArray());
                error = null;
                return true;
            }

            public void Tick()
            {
                Ticks++;
                ClientAuthStatusChanged?.Invoke(-1, "none");
            }

            public bool End(out string error)
            {
                Ended = true;
                error = null;
                return true;
            }

            public void Dispose()
            {
                Disposed = true;
            }

            public void RaiseMessageToClient(long handle, params byte[] message)
            {
                MessageToClient?.Invoke(handle, new ArraySegment<byte>(message));
            }

            public void RaiseAction(long handle, bool remove, string description)
            {
                ClientActionRequired?.Invoke(handle, remove, description);
            }
        }
    }
}
