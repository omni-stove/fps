using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 戦闘のモジュールが、外れた弾の通り道と被弾をスタミナ・よろけ・制圧のモジュールへ渡すことを確かめる
    public sealed class ServerPlayerStatusCombatTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _hitDamage = 10;
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerPlayerStatus _status;
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
        public void AMissThatPassesBesideAnEnemySuppressesThemButNotTheShooter()
        {
            Start(On());
            Join(1);
            Join(2);
            Step(0.2f);

            FireBeside(1, 2, 1f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the shot misses");
            Assert.That(_status.TryGetMeters(2, out var target), Is.True);
            Assert.That(target.SuppressionLevel, Is.GreaterThan(0f), _log.ToString());
            Assert.That(_status.TryGetMeters(1, out var shooter), Is.True);
            Assert.That(shooter.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void AHitIsNotANearMiss()
        {
            Start(On());
            Join(1);
            Join(2);
            Step(0.2f);

            FireBeside(1, 2, 0f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _hitDamage), _log.ToString());
            Assert.That(_status.TryGetMeters(2, out var target), Is.True);
            Assert.That(target.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void WithSuppressionOffAMissDoesNothing()
        {
            Start(ActionRules.Default.WithDowned(Downed.Off).WithStagger(Stagger.On));
            Join(1);
            Join(2);
            Step(0.2f);

            FireBeside(1, 2, 1f);

            Assert.That(_status.TryGetMeters(2, out var target), Is.True);
            Assert.That(target.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void AMeleeHitStaggersTheVictim()
        {
            Start(On());
            Join(1);
            Join(2);
            Step(0.2f);

            var outcome = _combat.DealDamage(1, 2, 20, "knife", HitZone.Body, null, DamageKind.Melee);

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(_status.TryGetMeters(2, out var victim), Is.True);
            Assert.That(victim.StaggerKind, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(_log.ToString(), Does.Contain("[Stagger] staggered player=2 kind=Stumble"));
        }

        [Test]
        public void AKillingBlowDoesNotStagger()
        {
            Start(On());
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, "knife", HitZone.Body, null, DamageKind.Melee);

            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Stagger] staggered player=2"));
        }

        [Test]
        public void AShotHitOnTheBodyDoesNotStaggerWithThePlayersDefaultTuning()
        {
            Start(On());
            Join(1);
            Join(2);
            Step(0.2f);

            FireBeside(1, 2, 0f);

            Assert.That(_status.TryGetMeters(2, out var victim), Is.True);
            Assert.That(victim.StaggerKind, Is.EqualTo(StaggerKind.None));
            Assert.That(victim.StaggerJudge.AccumulatedDamage, Is.EqualTo(_hitDamage), "the hit still counts towards a later stagger");
        }

        private static ActionRules On()
        {
            return ActionRules.Default.WithDowned(Downed.Off).WithStamina(Stamina.On).WithStagger(Stagger.On).WithSuppression(Suppression.On);
        }

        private void Start(ActionRules actions)
        {
            var rifle = new WeaponSpec(
                TestRules.RifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Auto, 0.1f),
                impact: new ImpactSpec(_hitDamage));
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
            _combat = new ServerCombat(_rules, movement);
            _status = new ServerPlayerStatus(rules, sides: _rules);
            movement.InputGate = _status;
            _combat.PlayerStatus = _status;
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_status);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        // 相手の体の中心から横 (z) へ offset ずらした点を狙う。相手は x の向こうに立つので、横にずらせば弾は相手の脇を抜ける
        private void FireBeside(int shooter, int target, float offset)
        {
            Assert.That(_context.Bodies.TryGet(shooter, out var from), Is.True);
            Assert.That(_context.Bodies.TryGet(target, out var to), Is.True);
            var eye = from.EyePosition;
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = to.Center + new Vector3(0f, 0f, offset) - eye,
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
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }
    }
}
