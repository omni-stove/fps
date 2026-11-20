using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // -bots 4 で動かした専用サーバーに UDP のクライアントを 2 人つなぎ、2 人とも互いの名前と Bot の名前を受け取ることを確かめる
    [NonParallelizable]
    public sealed class PlayerNamesEndToEndTests
    {
        private const string _version = "0.2.0";
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        private StringWriter _log;
        private StringWriter _stdout;
        private UdpMatchTransport _transport;
        private TestClient _alice;
        private TestClient _bob;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _stdout = new StringWriter();
            _transport = new UdpMatchTransport();
            _alice = new TestClient();
            _bob = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _alice.Dispose();
            _bob.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
        }

        [Test]
        public void TwoClientsGetEachOthersNamesAndTheBotNames()
        {
            Start();
            Connect(_alice, "alice-device", "Alice");
            Connect(_bob, "bob-device", "Bob");
            var aliceId = _alice.Accepted.PlayerId;
            var bobId = _bob.Accepted.PlayerId;

            Pump(() => HasEveryone(_alice) && HasEveryone(_bob));

            foreach (var client in new[] { _alice, _bob })
            {
                Assert.That(client.Names[aliceId], Is.EqualTo("Alice"));
                Assert.That(client.Names[bobId], Is.EqualTo("Bob"));
                var bots = client.Names.Keys.Where(BotRoster.IsBotId).ToList();
                Assert.That(bots, Is.Not.Empty, "the server was started with bots");
                foreach (var bot in bots)
                {
                    Assert.That(client.Names[bot], Is.EqualTo(BotRoster.KillFeedName(bot)));
                }
            }

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Names] player=" + Text(aliceId) + " name=\"Alice\""));
            Assert.That(log, Does.Contain("[Names] player=" + Text(bobId) + " name=\"Bob\""));
        }

        [Test]
        public void TheOneWhoStaysLearnsTheOtherLeft()
        {
            Start();
            Connect(_alice, "alice-device", "Alice");
            Connect(_bob, "bob-device", "Bob");
            var bobId = _bob.Accepted.PlayerId;
            Pump(() => HasEveryone(_alice) && HasEveryone(_bob));

            _bob.Disconnect("leaving");

            Pump(() => !_alice.Names.ContainsKey(bobId) && HasEveryone(_alice));
            Assert.That(_log.ToString(), Does.Contain("[Names] player=" + Text(bobId) + " left roster"));
        }

        // サーバーの試合にいる全員 (人と Bot) が、受け取った最後の名簿にちょうど載っている
        private bool HasEveryone(TestClient client)
        {
            var players = new HashSet<int>(_server.Match.Players.Select(player => player.PlayerId));
            return players.Any(BotRoster.IsBotId) && players.SetEquals(client.Names.Keys);
        }

        private void Connect(TestClient client, string key, string displayName)
        {
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest(_version, key, displayName: displayName);
            Pump(() => client.Accepted != null);
        }

        private void Start()
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(
                    Args("-buildVersion", _version, "-bots", "4"),
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);

            var rules = GameRules.Default.WithIdleKickSeconds(0f);
            var data = TestRules.Data(rules);
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-25f, 1.1f, -25f), Team.Alpha),
                    new SpawnPoint(new Vector3(-20f, 1.1f, -25f), Team.Alpha),
                    new SpawnPoint(new Vector3(25f, 1.1f, 25f), Team.Bravo),
                    new SpawnPoint(new Vector3(20f, 1.1f, 25f), Team.Bravo),
                },
                Array.Empty<ControlPointArea>());
            var map = BotTestMaps.Flat(points);
            var runtime = new ServerRuntime(
                TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), Array.Empty<string>(), Array.Empty<string>(), () => _transport);
            var modules = new List<IServerModule>();
            Assert.That(ServerModules.Create(launch, runtime, data, modules, out var problem), Is.True, problem);
            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            _server = new MatchServer(
                launch, _transport, host, map, _log, new ServerEventWriter(true, _stdout, _log), () => _time, data.Settings.RestartDelaySeconds, data);
            _server.Start();
        }

        // 1 tick ごとに眠ると時間がかかるので、通信を回すのは数 tick おきにする
        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            var ticks = 0;
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _alice.Poll();
                _bob.Poll();
                if (until())
                {
                    return;
                }

                if (++ticks % 8 == 0)
                {
                    Thread.Sleep(1);
                }
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _log);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
