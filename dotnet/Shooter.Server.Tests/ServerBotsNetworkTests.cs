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
    // -bots 4 で動かしたサーバーに UDP のクライアントを 1 人つなぎ、Bot が動き回って撃ち合い、kill の行と位置のログが出るまでを確かめる
    [NonParallelizable]
    public sealed class ServerBotsNetworkTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        private StringWriter _log;
        private StringWriter _stdout;
        private UdpMatchTransport _transport;
        private TestClient _client;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _stdout = new StringWriter();
            _transport = new UdpMatchTransport();
            _client = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _client.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
        }

        [Test]
        public void BotsRoamAndShootEachOtherWhileAClientWatches()
        {
            Start();
            _client.Connect(_port);
            Pump(() => _client.Connected);
            _client.SendRequest("0.2.0", "watcher");
            Pump(() => _client.Accepted != null);

            Pump(() => BotRows().Count == 3);
            var firstSeen = BotRows().ToDictionary(row => row.PlayerId, row => row.Position);

            Pump(() => KillLines().Any(IsByBot));

            var log = _log.ToString();
            var kill = KillLines().First(IsByBot);
            TestContext.Out.WriteLine(kill);
            foreach (var line in log.Split('\n').Where(line => line.StartsWith("[Bot] pos", StringComparison.Ordinal)).Take(12))
            {
                TestContext.Out.WriteLine(line);
            }

            Assert.That(log, Does.Contain("[Bot] added player=" + Text(BotRoster.FirstBotPlayerId)));
            Assert.That(log, Does.Match(@"\[Net\] hit attacker=10000000\d\d"));
            var moved = BotRows().Count(row => firstSeen.TryGetValue(row.PlayerId, out var first) && Vector3.Distance(first, row.Position) > 1f);
            Assert.That(moved, Is.GreaterThan(0), "no bot moved in the snapshots the client received");
            foreach (var botId in firstSeen.Keys)
            {
                Assert.That(log, Does.Contain("[Bot] pos player=" + Text(botId)));
            }
        }

        private List<PlayerSnapshot> BotRows()
        {
            var rows = new List<PlayerSnapshot>();
            var snapshot = _client.LatestSnapshot;
            if (snapshot == null)
            {
                return rows;
            }

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                if (BotRoster.IsBotId(snapshot.Players[i].PlayerId))
                {
                    rows.Add(snapshot.Players[i]);
                }
            }

            return rows;
        }

        private static bool IsByBot(string killLine)
        {
            var match = System.Text.RegularExpressions.Regex.Match(killLine, "\"killerId\":\"(\\d+)\"");
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var killer)
                && BotRoster.IsBotId(killer);
        }

        private IEnumerable<string> KillLines()
        {
            return _stdout.ToString().Split('\n').Where(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal));
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
                    Args("-buildVersion", "0.2.0", "-log-format", "json", "-bots", "4"),
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);

            var rules = GameRules.Default.WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
            var data = TestRules.Data(rules);

            // 見ている人は離れた隅に出し、Bot どうしが巡回の途中で出会うようにする
            var points = new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-25f, 1.1f, -25f), Team.Alpha),
                    new SpawnPoint(new Vector3(-8f, 1.1f, 15f), Team.Alpha),
                    new SpawnPoint(new Vector3(8f, 1.1f, 15f), Team.Bravo),
                    new SpawnPoint(new Vector3(0f, 1.1f, 5f), Team.Bravo),
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

        // 1 tick ごとに眠ると数十秒ぶんの試合に時間がかかるので、通信を回すのは数 tick おきにする
        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            var ticks = 0;
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _client.Poll();
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

            Assert.Fail("timed out waiting for the server and the client\n" + _log);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
