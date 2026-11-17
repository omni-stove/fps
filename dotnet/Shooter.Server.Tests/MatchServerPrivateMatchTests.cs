using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    [NonParallelizable]
    public sealed class MatchServerPrivateMatchTests
    {
        private const float _tickSeconds = 1f / 60f;
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
        public void APrivateServerLetsInTheRightCodeInAnyCase()
        {
            Start(Private("FRIENDS"));

            var client = Request("device1", "friends");

            Assert.That(client.Accepted, Is.Not.Null, client.DisconnectReason);
            Assert.That(_server.Match.HumanCount, Is.EqualTo(1));
        }

        [Test]
        public void APrivateServerRejectsAClientWithoutACode()
        {
            Start(Private("FRIENDS"));

            var client = Request("device1", "");

            Assert.That(client.Accepted, Is.Null);
            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.JoinCodeMismatchDetail)));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: no join code for the private match"));
            Assert.That(_server.Match.HumanCount, Is.Zero);
        }

        [Test]
        public void APrivateServerRejectsAWrongCodeWithoutLoggingIt()
        {
            Start(Private("FRIENDS"));

            var client = Request("device1", "GUESS1");

            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.JoinCodeMismatchDetail)));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: wrong join code for the private match"));
            Assert.That(_log.ToString(), Does.Not.Contain("GUESS1"));
        }

        [Test]
        public void AMalformedCodeCountsAsNoCode()
        {
            Start(Private("FRIENDS"));

            var client = Request("device1", "has/slash");

            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.JoinCodeMismatchDetail)));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: no join code for the private match"));
        }

        [Test]
        public void APublicServerIgnoresTheCode()
        {
            Start(TestRules.Data());

            Assert.That(Request("device1", "ANY").Accepted, Is.Not.Null);
            Assert.That(Request("device2", "").Accepted, Is.Not.Null);
        }

        [Test]
        public void TheVersionIsCheckedBeforeTheCode()
        {
            Start(Private("FRIENDS"));

            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.1.0", "device1", MatchProtocol.Version, "", "FRIENDS");
            Pump(() => client.Disconnected || client.Accepted != null);

            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.VersionMismatchReason("0.2.0")));
        }

        private static ServerRulesData Private(string joinCode)
        {
            return TestRules.Data(settings: new NetMatchSettings(privateMatch: true, joinCode: joinCode));
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

        private TestClient Request(string key, string joinCode)
        {
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key, MatchProtocol.Version, "", joinCode);
            Pump(() => client.Disconnected || client.Accepted != null);
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
