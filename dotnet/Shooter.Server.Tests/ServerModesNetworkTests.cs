using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ServerModules が作るモジュール一式を UDP で待ち受けさせ、2 人のクライアントで目的のある試合を決着まで通す。
    // 近接は InputButtons.Melee の入力だけで振らせ、Gun Game は撃って段を登らせる
    [NonParallelizable]
    public sealed class ServerModesNetworkTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        // 押してから振り終えて待ち終えるまで (0.5 + 0.4 秒) より長く空けて押し直す
        private const int _meleeRepeatTicks = 60;

        private const int _shotIntervalTicks = 15;

        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

        private StringWriter _log;
        private StringWriter _stdout;
        private UdpMatchTransport _transport;
        private TestClient _first;
        private TestClient _second;
        private ushort _port;
        private float _now;
        private int _inputTick;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _stdout = new StringWriter();
            _transport = new UdpMatchTransport();
            _first = new TestClient();
            _second = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
            _inputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _first.Dispose();
            _second.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
        }

        [Test]
        public void AFreeForAllIsWonByAMeleeKillAndTheVictimGetsAKillCamClip()
        {
            var rules = Rules(MatchObjective.FreeForAll).WithStreaks(Streaks.On);
            var settings = new NetMatchSettings(minEnemySpawnDistance: 0f, freeForAll: new FreeForAllSpec(1, 300f, 3f, 1));
            Start(rules, settings, new Vector3(-0.75f, 1.6f, 0f), new Vector3(0.75f, 1.6f, 0f));
            Join(_first, "first");
            Join(_second, "second");
            var attackerId = _first.Accepted.PlayerId;
            var victimId = _second.Accepted.PlayerId;
            Pump(() => Row(_first, attackerId).HasValue && Row(_first, victimId).HasValue && Row(_first, victimId).Value.Health > 0);
            var synced = _first.LatestSnapshot.Match.Rules;
            Assert.That(synced.Melee, Is.EqualTo(Melee.On), "the client learns that melee is on from the snapshot");
            Assert.That(synced.Streaks, Is.EqualTo(Streaks.On));

            Pump(() =>
            {
                var press = _inputTick % _meleeRepeatTicks == 0;
                SendInput(_first, FacingYaw(Row(_first, attackerId).Value.Position, Row(_first, victimId).Value.Position), press ? InputButtons.Melee : InputButtons.None);
                return _first.LatestSnapshot.Match.State == GameModeState.Ended;
            });

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Melee] swing id=" + Text(attackerId)));
            Assert.That(log, Does.Contain("[Melee] hit id=" + Text(attackerId) + " target=" + Text(victimId) + " dmg=" + Text(MeleeSpec.DefaultDamage)));
            Assert.That(_stdout.ToString(), Does.Contain("\"type\":\"kill\""));
            var match = _first.LatestSnapshot.Match;
            Assert.That(match.Objective, Is.EqualTo(MatchObjective.FreeForAll));
            Assert.That(match.HasWinner, Is.True);
            Assert.That(match.Winner, Is.EqualTo(Row(_first, attackerId).Value.Team), "the one who reached the kill limit wins");
            Assert.That(Row(_first, attackerId).Value.Kills, Is.EqualTo(1));
            Assert.That(Row(_first, attackerId).Value.StreakKills, Is.EqualTo(1), "the melee kill counts toward the streak");
            Assert.That(Row(_first, victimId).Value.Deaths, Is.EqualTo(1));
            Pump(() => _second.Received.Contains(MessageIds.KillCamClip));
            Assert.That(_second.Received, Does.Contain(MessageIds.Killed));
            Assert.That(_first.Received, Does.Not.Contain(MessageIds.KillCamClip), "only the victim gets the clip");
            Assert.That(_log.ToString(), Does.Contain("[KillCam] sent killer=" + Text(attackerId) + " victim=" + Text(victimId)));
        }

        [Test]
        public void AGunGameIsWonByClimbingEveryTierAndTheKillerIsRearmedAtOnce()
        {
            var settings = new NetMatchSettings(
                gunGame: new GunGameSpec(600f, 0.5f, new[] { TestRules.RifleId, TestRules.HandgunId }, Array.Empty<string>(), true));
            Start(Rules(MatchObjective.GunGame).WithStreaks(Streaks.Off), settings, new Vector3(-5f, 1.6f, 0f), new Vector3(5f, 1.6f, 0f));
            Join(_first, "first");
            Join(_second, "second");
            var shooterId = _first.Accepted.PlayerId;
            var targetId = _second.Accepted.PlayerId;
            Pump(() => Row(_first, shooterId).HasValue && Row(_first, targetId).HasValue && Row(_first, targetId).Value.Health > 0
                && Row(_first, shooterId).Value.WeaponId == TestRules.RifleId);
            Assert.That(_first.LatestSnapshot.Match.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(Row(_first, shooterId).Value.GunGameTier, Is.Zero);

            FireUntil(shooterId, targetId, () => Row(_first, shooterId).Value.GunGameTier == 1);
            Pump(() => Row(_first, shooterId).Value.WeaponId == TestRules.HandgunId);
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame rearm player=" + Text(shooterId) + " weapon=" + TestRules.HandgunId));
            Assert.That(Row(_first, targetId).Value.WeaponId, Is.EqualTo(TestRules.RifleId), "the victim stays on the first tier");

            FireUntil(shooterId, targetId, () => _first.LatestSnapshot.Match.State == GameModeState.Ended);

            var match = _first.LatestSnapshot.Match;
            Assert.That(match.HasWinner, Is.True);
            Assert.That(match.Winner, Is.EqualTo(Row(_first, shooterId).Value.Team));
            Assert.That(Row(_first, shooterId).Value.GunGameTier, Is.EqualTo(2), "the finisher is sent at the tier count");
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=finish winner=" + Text(shooterId)));
            Assert.That(match.Rules.Streaks, Is.EqualTo(Streaks.Off));
        }

        private static GameRules Rules(MatchObjective objective)
        {
            return GameRules.Default
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithDeathCamera(DeathCamera.KillCam)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithMelee(Melee.On))
                .WithObjective(objective);
        }

        // 撃つ間合いは Semi のハンドガンの間隔 (0.2 秒) より空け、連射の制限で捨てられないようにする
        private void FireUntil(int shooterId, int targetId, Func<bool> done)
        {
            var shotIndex = 0;
            var lastShotTick = -100;
            Pump(() =>
            {
                var snapshot = _first.LatestSnapshot;
                var shooter = Row(_first, shooterId);
                var target = Row(_first, targetId);
                if (shooter.HasValue && target.HasValue && target.Value.Health > 0 && snapshot.Tick - lastShotTick >= _shotIntervalTicks)
                {
                    lastShotTick = snapshot.Tick;
                    var eye = shooter.Value.Position + new Vector3(0f, ServerMotorSettings.DefaultEyeY, 0f);
                    _first.Send(new FireRequestMessage
                    {
                        WeaponId = shooter.Value.WeaponId,
                        Origin = eye,
                        Direction = target.Value.Position - eye,
                        ClientTick = snapshot.Tick,
                        InterpolationAlpha = 0f,
                        ShotIndex = shotIndex++,
                        ShotGeneration = 0,
                    });
                }

                return done();
            });
        }

        private void Start(GameRules rules, NetMatchSettings settings, Vector3 alphaSpawn, Vector3 bravoSpawn)
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

            var data = TestRules.Data(rules, settings, name: rules.Objective.ToString());
            var points = new NetMapPoints(
                new[] { new SpawnPoint(alphaSpawn, Team.Alpha), new SpawnPoint(bravoSpawn, Team.Bravo) },
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

        private void SendInput(TestClient client, float yaw, InputButtons buttons)
        {
            var input = new InputMessage();
            input.Add(new InputFrame(++_inputTick, Vector2.Zero, yaw, buttons));
            client.Send(input, TransportChannel.Unreliable);
        }

        // ServerMelee は yaw から前を (sin, 0, cos) で取るので、相手の向きをその形の角度にする
        private static float FacingYaw(Vector3 from, Vector3 to)
        {
            return MathF.Atan2(to.X - from.X, to.Z - from.Z);
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
                _first.Poll();
                _second.Poll();
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
