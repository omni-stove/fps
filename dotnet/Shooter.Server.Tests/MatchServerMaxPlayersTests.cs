using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos.Tests;

namespace Shooter.Server.Tests
{
    [NonParallelizable]
    public sealed class MatchServerMaxPlayersTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _endpoint = "203.0.113.10:7777";
        private const string _lobbyServer = "0123456789abcdef0123456789abcdef";
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private static readonly string[] _args = { "-buildVersion", "0.2.0" };

        private readonly List<TestClient> _clients = new List<TestClient>();
        private StringWriter _log;
        private UdpMatchTransport _transport;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void ThirdClientIsRejectedAsFullWhenTheMatchTakesTwo()
        {
            Start(TestRules.Data(settings: new NetMatchSettings(maxPlayers: 2)));
            Join("device1");
            Join("device2");

            var third = Request("device3");

            Assert.That(third.Accepted, Is.Null);
            Assert.That(third.DisconnectReason, Is.EqualTo("shooter:rejected:server-full"));
            Assert.That(DisconnectNotice.Classify(third.DisconnectReason, false, "0.2.0").Detail, Is.EqualTo(DisconnectNotice.ServerFullDetail));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 3: the match is full (2/2 players)"));
            Assert.That(_server.Match.HumanCount, Is.EqualTo(2));
        }

        [Test]
        public void ASeatFreedByALeaverCanBeTaken()
        {
            Start(TestRules.Data(settings: new NetMatchSettings(maxPlayers: 1)));
            var first = Join("device1");
            Assert.That(Request("device2").Accepted, Is.Null);

            first.Dispose();
            Pump(() => _server.Match.HumanCount == 0);
            var next = Request("device3");

            Assert.That(next.Accepted, Is.Not.Null, next.DisconnectReason);
            Assert.That(_server.Match.HumanCount, Is.EqualTo(1));
        }

        [Test]
        public void ReconnectingIntoAFullMatchTakesBackItsOwnSeat()
        {
            Start(TestRules.Data(settings: new NetMatchSettings(maxPlayers: 2)));
            var first = Join("device1");
            Join("device2");

            var again = Request("device1");
            Pump(() => first.Disconnected);

            Assert.That(again.Accepted, Is.Not.Null, again.DisconnectReason);
            Assert.That(first.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(MatchServer.ReconnectedDetail)));
            Assert.That(_server.Match.HumanCount, Is.EqualTo(2));
        }

        [Test]
        public void WithoutARulesFileTheServerTakesEightPlayers()
        {
            Start(null);

            Assert.That(_server.MaxPlayers, Is.EqualTo(8));
            for (var i = 1; i <= 8; i++)
            {
                Join("device" + i.ToString(CultureInfo.InvariantCulture));
            }

            Assert.That(Request("device9").DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.ServerFullDetail)));
        }

        [Test]
        public void TheLobbyIsCreatedWithTheMatchsMaxPlayers()
        {
            using var backend = new FakeLobbyBackend();
            var match = new ServerMatch(0f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            using var module = new ServerLobbyModule(_ => backend, _endpoint, () => _now, _ => { });
            module.Start(new ServerContext(
                new NetLaunchOptions(
                    NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, _endpoint),
                "0.3.0",
                _log,
                TestMaps.Data(),
                match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(settings: new NetMatchSettings(maxPlayers: 2))));

            module.Tick(1, _tickSeconds);
            backend.CompleteLogin(_lobbyServer);

            Assert.That(backend.CreatedMaxMembers, Is.EqualTo(2));
            Assert.That(module.Registration.Registrar.MaxPlayers, Is.EqualTo(2));
        }

        private void Start(ServerRulesData rules)
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(
                    _args,
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);
            _server = new MatchServer(
                launch,
                _transport,
                new ServerHost(),
                TestMaps.Data(),
                _log,
                new ServerEventWriter(false, _log, _log),
                () => _time,
                ServerMatch.DefaultRestartDelaySeconds,
                rules);
            _server.Start();
        }

        private TestClient Request(string key)
        {
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key, MatchProtocol.Version);
            Pump(() => client.Disconnected || client.Accepted != null);
            return client;
        }

        private TestClient Join(string key)
        {
            var client = Request(key);
            Assert.That(client.Accepted, Is.Not.Null, "the server did not approve the client: " + client.DisconnectReason);
            return client;
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                foreach (var client in _clients)
                {
                    client.Poll();
                }

                if (until())
                {
                    return;
                }

                Thread.Sleep(1);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and clients");
        }
    }
}
