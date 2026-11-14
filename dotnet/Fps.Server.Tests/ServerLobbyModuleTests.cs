using System;
using System.IO;
using Fps.Core;
using Fps.Protocol;
using Fps.Server.Eos;
using Fps.Server.Eos.Tests;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerLobbyModuleTests
    {
        private const string _server = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string _endpoint = "203.0.113.10:7777";

        private StringWriter _log;
        private UdpMatchTransport _transport;
        private FakeLobbyBackend _backend;
        private StubControl _control;
        private ServerMatch _match;
        private ServerLobbyModule _module;
        private float _now;
        private int _slept;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _backend = new FakeLobbyBackend();
            _control = new StubControl();
            _match = new ServerMatch(0f, _log, new ServerEventWriter(false, _log, _log), () => DateTime.UtcNow);
            _now = 0f;
            _slept = 0;
            _module = new ServerLobbyModule(
                _ => _backend,
                _endpoint,
                () => _now,
                milliseconds =>
                {
                    _slept += milliseconds;
                    _now += milliseconds / 1000f;
                });
        }

        [TearDown]
        public void TearDown()
        {
            _module.Dispose();
            _backend.Dispose();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void RegistersOnTheFirstTickNotAtStart()
        {
            Start();

            Assert.That(_backend.Logins, Is.Zero);

            Tick();

            Assert.That(_backend.Logins, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] registering server=" + _endpoint + " bucket=fps-server:0.3.0"));
        }

        [Test]
        public void PublishesTheConnectedPlayers()
        {
            Register();

            _match.Join(new ConnectionId(1), "device1");
            Tick();

            Assert.That(_backend.Updates, Has.Count.EqualTo(1));
            Assert.That(_backend.Updates[0][0].Number, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] players 0 -> 1"));
        }

        [Test]
        public void ClosesTheLobbyWhenTheServerStartsDraining()
        {
            Register();

            _control.IsDraining = true;
            Tick();

            Assert.That(_backend.Destroys, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] unregistering id=l1"));

            _match.Join(new ConnectionId(1), "device1");
            _backend.CompleteDestroy();
            Tick();
            Assert.That(_backend.Updates, Is.Empty);
            Assert.That(_backend.Creates, Is.EqualTo(1));
        }

        [Test]
        public void FailsTheServerWithExitSevenWhenNeverRegistered()
        {
            Start();
            Tick();
            _backend.CompleteLogin(null, "invalid client credentials");

            _now = ServerLobby.TimeoutSeconds;
            Tick();

            Assert.That(_control.ExitCode, Is.EqualTo(ServerProgram.ExitLobbyFailed));
            Assert.That(_control.Failures, Has.Count.EqualTo(1));
            Assert.That(_control.Failures[0], Does.StartWith("[Lobby] the server is not registered after 60s"));
        }

        [Test]
        public void StopWaitsForTheLobbyToCloseAndReleasesTheBackend()
        {
            Register();
            _backend.OnTick = () =>
            {
                if (_backend.PendingDestroy != null && _slept >= 50)
                {
                    _backend.CompleteDestroy();
                }
            };

            _module.Stop();

            Assert.That(_backend.Destroys, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] closing the server lobby before quitting (up to 3s)"));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] closed id=l1"));
            Assert.That(_log.ToString(), Does.Not.Contain("did not close"));
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void StopGivesUpAfterTheWait()
        {
            Register();

            _module.Stop();

            Assert.That(_now, Is.GreaterThanOrEqualTo(ServerLobbyRegistration.CloseWaitSeconds));
            Assert.That(_log.ToString(), Does.Contain("[Lobby] the server lobby did not close within 3s; quitting anyway"));
            Assert.That(_backend.Disposed, Is.True);
        }

        [Test]
        public void StopBeforeAnyTickDoesNotWait()
        {
            Start();

            _module.Stop();

            Assert.That(_slept, Is.Zero);
            Assert.That(_backend.Logins, Is.Zero);
            Assert.That(_backend.Disposed, Is.True);
        }

        private void Start()
        {
            _module.Start(new ServerContext(
                new NetLaunchOptions(
                    NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, _endpoint),
                "0.3.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(_transport),
                _control));
        }

        private void Register()
        {
            Start();
            Tick();
            _backend.CompleteLogin(_server);
            _backend.CompleteCreate(new LobbyInfo("l1", _server, ServerLobby.MaxPlayers, new[] { _server }));
        }

        private void Tick()
        {
            _module.Tick(1, 1f / 60f);
        }
    }
}
