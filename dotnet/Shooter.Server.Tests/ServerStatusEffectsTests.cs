using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 燃やす武器で撃ち、サーバーが燃焼を毎 tick 進めてダメージを入れ、かけた人に帰属させて知らせることを確かめる
    public sealed class ServerStatusEffectsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _hitDamage = 10;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly float[] _extendedThenEnded = { 2f, 2f, 0f };
        private static readonly float[] _startedThenEnded = { 5f, 0f };

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private int _shotIndex;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _shotIndex = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void TheBurnKeepsDamagingUntilItRunsOutAndEveryoneHearsItStartAndEnd()
        {
            Start(new StatusEffectSpec(2f, 0.5f, 5));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, 2);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _hitDamage), _log.ToString());

            Step(1.02f);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _hitDamage - (2 * 5)), _log.ToString());

            Step(2f);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _hitDamage - (4 * 5)), "it stops when the time runs out");

            for (var receiver = 1; receiver <= 2; receiver++)
            {
                var status = _transport.Read<StatusEffectMessage>(receiver, Copy);
                Assert.That(status, Has.Count.EqualTo(2));
                Assert.That(status[0].TargetId, Is.EqualTo(2));
                Assert.That(status[0].KindId, Is.EqualTo(StatusEffectKinds.Burn));
                Assert.That(status[0].RemainingSeconds, Is.EqualTo(2f));
                Assert.That(status[1].RemainingSeconds, Is.Zero, "a remaining time of 0 tells the effect ended");
            }

            Assert.That(_transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { Amount = m.Amount }).Count(m => m.Amount == 5), Is.EqualTo(4));
            Assert.That(_log.ToString(), Does.Contain("[StatusEffect] tick source=1 victim=2 kind=burn dmg=5 outcome=Damaged"));
        }

        [Test]
        public void HittingAgainExtendsTheBurnInsteadOfStackingIt()
        {
            Start(new StatusEffectSpec(2f, 0.5f, 5));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, 2);
            Step(1f);
            FireAt(1, 2);
            Step(2.02f);

            // 当たり 2 回と、1 回目から 3 秒燃え続けた 6 回
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (2 * _hitDamage) - (6 * 5)), _log.ToString());
            var status = _transport.Read<StatusEffectMessage>(2, Copy);
            Assert.That(status.Select(s => s.RemainingSeconds), Is.EqualTo(_extendedThenEnded));
        }

        [Test]
        public void ABurnKillIsCreditedToTheShooterAndTheirWeapon()
        {
            Start(new StatusEffectSpec(3f, 0.5f, 50));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, 2);
            Step(1.1f);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var killed = _transport.Read<KilledMessage>(2, m => new KilledMessage { KillerId = m.KillerId, VictimId = m.VictimId, WeaponId = m.WeaponId }).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.VictimId, Is.EqualTo(2));
            Assert.That(killed.WeaponId, Is.EqualTo(TestRules.RifleId));
            var status = _transport.Read<StatusEffectMessage>(1, Copy);
            Assert.That(status.Last().RemainingSeconds, Is.Zero, "dying puts the fire out");
        }

        [Test]
        public void ALeavingPlayerStopsBurning()
        {
            Start(new StatusEffectSpec(5f, 0.5f, 1));
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, 2);

            _match.Leave(new ConnectionId(2));
            Tick();

            var status = _transport.Read<StatusEffectMessage>(1, Copy);
            Assert.That(status.Select(s => s.RemainingSeconds), Is.EqualTo(_startedThenEnded));
        }

        private void Start(StatusEffectSpec burn)
        {
            var rifle = new WeaponSpec(
                TestRules.RifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Auto, 0.1f),
                impact: new ImpactSpec(_hitDamage, statusEffect: burn));
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var file = new NetMatchFile(
                "TeamDeathmatch",
                rules,
                NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle } }),
                new[] { rifle },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { rifle }));
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, null);
            var data = new ServerRulesData("TeamDeathmatch", file, PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(new ServerCombat(_rules, movement));
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void FireAt(int shooter, int target)
        {
            Assert.That(_context.Bodies.TryGet(shooter, out var from), Is.True);
            Assert.That(_context.Bodies.TryGet(target, out var to), Is.True);
            var eye = from.EyePosition;
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = to.Center - eye,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
            });
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private static StatusEffectMessage Copy(StatusEffectMessage m)
        {
            return new StatusEffectMessage { TargetId = m.TargetId, KindId = m.KindId, RemainingSeconds = m.RemainingSeconds };
        }
    }
}
