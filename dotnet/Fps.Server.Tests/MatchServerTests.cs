using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    [NonParallelizable]
    public sealed class MatchServerTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private readonly List<TestClient> _clients = new List<TestClient>();
        private List<string> _calls;
        private StringWriter _log;
        private StringWriter _json;
        private UdpMatchTransport _transport;
        private ushort _port;
        private float _now;
        private MatchServer _server;
        private RecordingModule _module;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _log = new StringWriter();
            _json = new StringWriter();
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
            _json.Dispose();
        }

        [Test]
        public void ApprovedClientJoinsTheMatchAndModulesSeeItsMessages()
        {
            Start();
            var client = Join("0.2.0");

            Assert.That(client.Accepted.PlayerId, Is.EqualTo(1));
            Assert.That(client.Accepted.ServerVersion, Is.EqualTo("0.2.0"));
            Assert.That(client.Accepted.TickRate, Is.EqualTo(TickClock.DefaultTickRate));
            Assert.That(_server.Match.PlayerCount, Is.EqualTo(1));
            Assert.That(_calls, Does.Contain("a.Connected#1"));
            Assert.That(_log.ToString(), Does.Contain("[Net] approved client 1 (version 0.2.0)"));
            Assert.That(_log.ToString(), Does.Contain("[Match] joined player=1 team=Alpha"));

            client.Send(new NetStatsProbeMessage());
            Pump(() => _calls.Contains("a.Message#1:" + MessageIds.NetStatsProbe));

            client.Dispose();
            Pump(() => _server.Match.PlayerCount == 0);
            Assert.That(_calls, Does.Contain("a.Disconnected#1"));
            Assert.That(_log.ToString(), Does.Contain("[Match] left player=1"));
        }

        [Test]
        public void VersionMismatchIsRejectedWithTheUnityServersLog()
        {
            Start();
            var client = Request("0.1.0");

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: version mismatch (client 0.1.0, server 0.2.0)"));
            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.VersionMismatchReason("0.2.0")));
            Assert.That(_server.Match.PlayerCount, Is.Zero);
            Assert.That(_calls, Has.None.StartsWith("a.Connected"));
        }

        [Test]
        public void UnreadableRequestIsRejected()
        {
            Start();
            var client = Request("not/a-version");

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: unreadable connection payload"));
            Assert.That(client.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail)));
        }

        [Test]
        public void ProtocolMismatchIsRejected()
        {
            Start();
            var client = Request("0.2.0", protocol: MatchProtocol.Version + 1);

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: protocol version mismatch"));
            Assert.That(client.DisconnectReason, Is.EqualTo(UdpMatchTransport.ProtocolMismatchReason));
        }

        [Test]
        public void MessagesBeforeTheRequestAndGarbageAreRejected()
        {
            Start();
            var early = ConnectClient();
            early.Send(new NetStatsProbeMessage());
            var garbage = ConnectClient();
            garbage.SendRaw(new byte[] { 0xFF, 0xFF, 0x01 });
            Pump(() => early.Disconnected && garbage.Disconnected);

            Assert.That(_log.ToString(), Does.Contain("before its connection request"));
            Assert.That(_log.ToString(), Does.Contain("unreadable connection request"));
            Assert.That(_server.Match.PlayerCount, Is.Zero);
        }

        [Test]
        public void SilentClientIsRejectedAfterTheApprovalTimeout()
        {
            Start();
            _server.ApprovalTimeoutSeconds = 0.5f;
            var client = ConnectClient();

            Pump(() => client.Disconnected);

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 1: no connection request within 1s"));
        }

        [Test]
        public void ReconnectingWithTheSameKeyDropsThePreviousConnection()
        {
            Start();
            var first = Join("0.2.0", "device1");
            var second = Join("0.2.0", "device1");
            Pump(() => first.Disconnected);

            Assert.That(first.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(MatchServer.ReconnectedDetail)));
            Assert.That(second.Disconnected, Is.False);
            Assert.That(_server.Match.PlayerCount, Is.EqualTo(1));
            Assert.That(_server.Match.TryGetPlayer(second.Accepted.PlayerId, out _), Is.True);
        }

        [Test]
        public void ModulesCanKickAPlayerAfterTheirTick()
        {
            Start();
            var client = Join("0.2.0");

            _module.KickOnNextTick = new ConnectionId((ulong)client.Accepted.PlayerId);
            Pump(() => client.Disconnected);

            Assert.That(client.DisconnectReason, Is.EqualTo("test-kick"));
            Assert.That(_log.ToString(), Does.Contain("[Net] dropped client 1: kicked by a"));
            Assert.That(_server.Match.PlayerCount, Is.Zero);
            Assert.That(_calls, Does.Contain("a.Disconnected#1"));
        }

        [Test]
        public void StopWithNobodyConnectedStopsAtOnceAndWritesTheJsonEvents()
        {
            Start(json: true);

            _server.RequestStop(_now);

            Assert.That(_server.ExitRequested, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] stop requested; stopping now because no players are connected"));
            var lines = _json.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Is.EqualTo(new[]
            {
                "{\"type\":\"server_started\",\"time\":\"2026-11-13T09:00:00.000Z\",\"version\":\"0.2.0\",\"transport\":\"udp\",\"endpoint\":\"127.0.0.1:" + _port + "\"}",
                "{\"type\":\"server_stopping\",\"time\":\"2026-11-13T09:00:00.000Z\"}",
            }));
        }

        [Test]
        public void DrainingRefusesNewPlayersAndStopsWhenTheLastOneLeaves()
        {
            Start();
            var player = Join("0.2.0", "device1");

            _server.RequestStop(_now);
            Assert.That(_server.IsDraining, Is.True);
            Assert.That(_server.ExitRequested, Is.False);
            Assert.That(_log.ToString(), Does.Contain(
                "[Net] stop requested; draining: refusing new players and waiting up to 600s for the match to end (players=1)"));

            var late = Request("0.2.0", "device2");
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected client 2: the server is draining before it stops"));
            Assert.That(late.DisconnectReason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail)));

            player.Dispose();
            Pump(() => _server.ExitRequested);
            Assert.That(_log.ToString(), Does.Contain("[Net] drain finished: no players are connected; stopping with exit code 0"));
        }

        [Test]
        public void DrainStopsWhenTheMatchEndsAndTellsEveryoneTheServerStopped()
        {
            Start();
            var alpha = Join("0.2.0", "device1");
            var bravo = Join("0.2.0", "device2");
            Assert.That(_server.Match.State, Is.EqualTo(GameModeState.Playing));
            _server.RequestStop(_now);
            Pump(() => true);
            Assert.That(_server.ExitRequested, Is.False);

            _server.Match.End(Team.Alpha);
            Pump(() => _server.ExitRequested);
            Assert.That(_log.ToString(), Does.Contain("[Net] drain finished: the match ended; stopping with exit code 0"));

            _server.Shutdown();
            Pump(() => alpha.Disconnected && bravo.Disconnected);

            Assert.That(alpha.DisconnectReason, Is.EqualTo(DisconnectNotice.ServerStoppedReason));
            Assert.That(bravo.DisconnectReason, Is.EqualTo(DisconnectNotice.ServerStoppedReason));
            Assert.That(_log.ToString(), Does.Contain("[Net] server stopping; told 2 client(s) and disconnected them"));
            Assert.That(_calls.TakeLast(3), Is.EqualTo(Strings("a.Disconnected#1", "a.Disconnected#2", "a.Stop")));
        }

        [Test]
        public void MatchThatEndsAndRestartsInOneTickStillEndsTheDrain()
        {
            Start(restartDelaySeconds: 0f);
            Join("0.2.0", "device1");
            Join("0.2.0", "device2");
            _server.RequestStop(_now);

            _server.Match.End(null);
            Pump(() => _server.ExitRequested);

            Assert.That(_server.Match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_log.ToString(), Does.Contain("drain finished: the match ended"));
        }

        [Test]
        public void SecondStopRequestStopsAtOnce()
        {
            Start();
            Join("0.2.0");

            _server.RequestStop(_now);
            _server.RequestStop(_now);

            Assert.That(_server.ExitRequested, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] drain cut short: a second stop request arrived; stopping now (players=1)"));
        }

        [Test]
        public void DrainGivesUpAfterTheTimeout()
        {
            Start(drainTimeout: "1");
            Join("0.2.0");
            Assert.That(_log.ToString(), Does.Contain("waits up to 1s for the match to end"));

            _server.RequestStop(_now);
            var stoppedAt = _now;
            Pump(() => _server.ExitRequested);

            Assert.That(_now - stoppedAt, Is.EqualTo(1f).Within(0.1f));
            Assert.That(_log.ToString(), Does.Contain("[Net] drain finished: the drain timeout (1s) passed"));
        }

        [Test]
        public void ZeroDrainTimeoutStopsAtOnce()
        {
            Start(drainTimeout: "0");
            Join("0.2.0");

            _server.RequestStop(_now);

            Assert.That(_server.ExitRequested, Is.True);
            Assert.That(_log.ToString(), Does.Contain("the server stops at once (-drain-timeout 0)"));
            Assert.That(_log.ToString(), Does.Contain("stopping now because the drain timeout (0s) passed"));
        }

        [Test]
        public void PortInUseFailsTheStartAndStopsTheModules()
        {
            using (var taken = new UdpClient(new IPEndPoint(IPAddress.Loopback, _port)))
            {
                _server = Create("-drain-timeout", "600");
                Assert.Throws<InvalidOperationException>(_server.Start);
            }

            Assert.That(_server.ListenFailed, Is.True);
            Assert.That(_calls, Is.EqualTo(Strings("a.Start", "a.Stop")));
        }

        private void Start(bool json = false, string drainTimeout = null, float restartDelaySeconds = ServerMatch.DefaultRestartDelaySeconds)
        {
            var args = new List<string> { "-buildVersion", "0.2.0" };
            if (json)
            {
                args.Add("-log-format");
                args.Add("json");
            }

            if (drainTimeout != null)
            {
                args.Add("-drain-timeout");
                args.Add(drainTimeout);
            }

            _server = Create(args.ToArray(), restartDelaySeconds);
            _server.Start();
            Assert.That(_log.ToString(), Does.Contain("[Net] server listening on 127.0.0.1:" + _port));
        }

        private MatchServer Create(params string[] args)
        {
            return Create(args, ServerMatch.DefaultRestartDelaySeconds);
        }

        private MatchServer Create(string[] args, float restartDelaySeconds)
        {
            var all = new List<string>(args);
            if (!all.Contains("-buildVersion"))
            {
                all.Add("-buildVersion");
                all.Add("0.2.0");
            }

            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(all, name => environment.TryGetValue(name, out var value) ? value : null, out var launch, out var error),
                Is.True,
                error);
            var host = new ServerHost();
            _module = new RecordingModule("a", _calls);
            host.Register(_module);
            var json = launch.Options.LogFormat == ServerLogFormat.Json;
            return new MatchServer(
                launch, _transport, host, TestMaps.Data(), _log, new ServerEventWriter(json, _json, _log), () => _time, restartDelaySeconds);
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Strings(params string[] values) => values;

        private TestClient ConnectClient()
        {
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            return client;
        }

        private TestClient Request(string version, string key = "device1", int protocol = MatchProtocol.Version)
        {
            var client = ConnectClient();
            client.SendRequest(version, key, (ushort)protocol);
            Pump(() => client.Disconnected || client.Accepted != null);
            return client;
        }

        private TestClient Join(string version, string key = "device1")
        {
            var client = Request(version, key);
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
