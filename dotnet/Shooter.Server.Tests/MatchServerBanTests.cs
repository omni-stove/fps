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
    public sealed class MatchServerBanTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private static readonly string[] _args = { "-buildVersion", "0.2.0" };

        private readonly List<TestClient> _clients = new List<TestClient>();
        private StringWriter _log;
        private UdpMatchTransport _transport;
        private TempDirectory _directory;
        private ushort _port;
        private float _now;
        private MatchServer _server;
        private ServerBanList _bans;
        private ServerAdminConsole _console;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _directory = new TempDirectory();
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
            _directory.Dispose();
            _log.Dispose();
        }

        [Test]
        public void TheTransportKnowsWhereAClientConnectedFrom()
        {
            Start("{ \"bans\": [] }");

            var client = Request("device1");

            Assert.That(client.Accepted, Is.Not.Null, client.DisconnectReason);
            Assert.That(_transport.TryGetRemoteAddress(new ConnectionId(1), out var address), Is.True);
            Assert.That(address, Is.EqualTo("127.0.0.1"));
            Assert.That(_transport.TryGetRemoteAddress(new ConnectionId(99), out _), Is.False);
        }

        [Test]
        public void ABannedAddressIsRefusedWithTheBannedReason()
        {
            Start("{ \"bans\": [ { \"address\": \"127.0.0.1\", \"reason\": \"aimbot\" } ] }");

            var client = Request("device1");

            Assert.That(client.Accepted, Is.Null);
            Assert.That(client.RejectedReason, Is.EqualTo("shooter:rejected:banned"));
            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.BannedDetail)));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: banned (address=127.0.0.1 reason=\"aimbot\")"));
            Assert.That(_server.Match.HumanCount, Is.Zero);
        }

        [Test]
        public void AClaimedProductUserIdIsNotEnoughToBeRefused()
        {
            const string bannedId = "0002aaaabbbbccccddddeeeeffff0001";
            Start("{ \"bans\": [ { \"productUserId\": \"" + bannedId + "\" } ] }");
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);

            client.Send(new ConnectRequestMessage
            {
                ClientVersion = "0.2.0",
                RejoinKey = "device1",
                DisplayName = "tester",
                ProtocolVersion = MatchProtocol.Version,
                ProductUserId = bannedId,
            });
            Pump(() => client.Disconnected || client.Accepted != null);

            Assert.That(client.Accepted, Is.Not.Null, client.DisconnectReason);
        }

        [Test]
        public void BanningFromTheConsoleDisconnectsThePlayerAndRefusesTheirReconnect()
        {
            Start(null);
            var first = Request("device1");
            Assert.That(first.Accepted, Is.Not.Null, first.DisconnectReason);

            _console.Execute("ban " + first.Accepted.PlayerId.ToString(CultureInfo.InvariantCulture) + " griefing");
            Pump(() => first.Disconnected);

            Assert.That(first.DisconnectReason, Is.EqualTo("shooter:rejected:banned"));
            Assert.That(_log.ToString(), Does.Contain("[Net] dropped client 1: banned from the admin console (address=127.0.0.1 banned_at=2026-11-18T09:00:00Z reason=\"griefing\")"));
            Assert.That(_server.Match.HumanCount, Is.Zero);
            Assert.That(File.ReadAllText(_bans.Path), Does.Contain("\"address\": \"127.0.0.1\""));

            var again = Request("device1");

            Assert.That(again.Accepted, Is.Null);
            Assert.That(again.RejectedReason, Is.EqualTo("shooter:rejected:banned"));

            _console.Execute("unban 127.0.0.1");
            var forgiven = Request("device1");

            Assert.That(forgiven.Accepted, Is.Not.Null, forgiven.DisconnectReason);
        }

        [Test]
        public void KickingFromTheConsoleDisconnectsWithoutBanning()
        {
            Start(null);
            var client = Request("device1");

            _console.Execute("kick " + client.Accepted.PlayerId.ToString(CultureInfo.InvariantCulture));
            Pump(() => client.Disconnected);

            Assert.That(client.DisconnectReason, Is.EqualTo("shooter:rejected:kicked"));
            Assert.That(_log.ToString(), Does.Contain("[Net] dropped client 1: kicked from the admin console"));
            Assert.That(Request("device1").Accepted, Is.Not.Null, "a kicked player can come back");
        }

        private void Start(string banFile)
        {
            var path = Path.Combine(_directory.Path, "bans.json");
            if (banFile != null)
            {
                File.WriteAllText(path, banFile);
            }

            Assert.That(ServerBanList.TryLoad(path, out _bans, out var error), Is.True, error);
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(_args, name => environment.TryGetValue(name, out var value) ? value : null, out var launch, out error),
                Is.True,
                error);
            var host = new ServerHost();
            _console = new ServerAdminConsole(null, _bans, () => _time);
            host.Register(_console);
            _server = new MatchServer(
                launch,
                _transport,
                host,
                TestMaps.Data(),
                _log,
                new ServerEventWriter(false, _log, _log),
                () => _time,
                ServerMatch.DefaultRestartDelaySeconds,
                TestRules.Data(),
                null,
                _bans);
            _server.Start();
        }

        private TestClient Request(string key)
        {
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key);
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
