using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // -log-format json で動かしたサーバーに UDP のクライアントを 2 人つなぎ、スナップショットで見えた相手を撃たせて kill の行が出るまでを確かめる
    [NonParallelizable]
    public sealed class ServerCombatNetworkTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

        private StringWriter _log;
        private StringWriter _stdout;
        private UdpMatchTransport _transport;
        private TestClient _shooter;
        private TestClient _target;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _stdout = new StringWriter();
            _transport = new UdpMatchTransport();
            _shooter = new TestClient();
            _target = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _shooter.Dispose();
            _target.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
        }

        [Test]
        public void TwoClientsShootingEachOtherProduceAKillLine()
        {
            Start();
            Join(_shooter, "shooter");
            Join(_target, "target");
            var shooterId = _shooter.Accepted.PlayerId;
            var targetId = _target.Accepted.PlayerId;
            Pump(() => Row(_shooter, targetId).HasValue && Row(_shooter, shooterId).HasValue && Row(_shooter, targetId).Value.Health > 0);

            var shotIndex = 0;
            var lastShotTick = -100;
            Pump(() =>
            {
                var snapshot = _shooter.LatestSnapshot;
                if (snapshot.Tick - lastShotTick >= 12)
                {
                    lastShotTick = snapshot.Tick;
                    var eye = Row(_shooter, shooterId).Value.Position + new Vector3(0f, ServerMotorSettings.DefaultEyeY, 0f);
                    _shooter.Send(new FireRequestMessage
                    {
                        WeaponId = snapshot.Players.Count > 0 ? Row(_shooter, shooterId).Value.WeaponId : "",
                        Origin = eye,
                        Direction = Row(_shooter, targetId).Value.Position - eye,
                        ClientTick = snapshot.Tick,
                        InterpolationAlpha = 0f,
                        ShotIndex = shotIndex++,
                        ShotGeneration = 0,
                    });
                }

                return _stdout.ToString().Contains("\"type\":\"kill\"", StringComparison.Ordinal);
            });

            var kill = _stdout.ToString().Split('\n').Single(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal));
            TestContext.Out.WriteLine(kill);
            Assert.That(kill, Does.Contain("\"killerId\":\"" + Text(shooterId) + "\"").And.Contain("\"victimId\":\"" + Text(targetId) + "\""));
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=" + Text(shooterId) + " victim=" + Text(targetId)));
            Pump(() => _target.Received.Contains(MessageIds.Killed) && _shooter.Received.Contains(MessageIds.HitConfirm));
            Assert.That(_target.Received, Does.Contain(MessageIds.DamageTaken));
            Assert.That(_target.Received, Does.Contain(MessageIds.Shot));
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
                    Args("-buildVersion", "0.2.0", "-log-format", "json"),
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);

            var rules = GameRules.Default.WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
            var data = TestRules.Data(rules);
            var points = new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", TestMaps.World(), TestMaps.NavMesh(), "memory", points);
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

        private void Join(TestClient client, string key)
        {
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key);
            Pump(() => client.Accepted != null);
        }

        private static PlayerSnapshot? Row(TestClient client, int playerId)
        {
            var snapshot = client.LatestSnapshot;
            if (snapshot == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                if (snapshot.Players[i].PlayerId == playerId)
                {
                    return snapshot.Players[i];
                }
            }

            return null;
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _shooter.Poll();
                _target.Poll();
                if (until())
                {
                    return;
                }

                Thread.Sleep(1);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _log);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
