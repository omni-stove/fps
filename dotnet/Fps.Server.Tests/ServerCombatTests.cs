using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 移動・ルール・戦闘を同じホストで動かし、射撃・巻き戻し・壁・爆発を確かめる
    public sealed class ServerCombatTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        // 床 (y=0.5 が上面) に足を付けた体の原点 (カプセルの中心、高さ 2)
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private static readonly int[] _wallHealth = { 20 };
        private static readonly bool[] _wallGrounded = { true };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private int _inputTick;
        private int _shotIndex;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _inputTick = 0;
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
            _json.Dispose();
        }

        [Test]
        public void ShotsKillAndTheKillIsWrittenAsJson()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 4; i++)
            {
                FireAt(1, Body(2).Center);
                Step(0.2f);
            }

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var kill = Lines(_json).Single(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal));
            Assert.That(kill, Does.Contain("\"killerId\":\"1\"").And.Contain("\"victimId\":\"2\"").And.Contain("\"teamKill\":\"false\""));
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=0.00 zone=Body dmg=25 hp=75 outcome=Damaged"));

            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Killed = m.Killed }).Last().Killed, Is.True);
            var taken = _transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { HasSource = m.HasSource, SourcePosition = m.SourcePosition, Amount = m.Amount });
            Assert.That(taken, Has.Count.EqualTo(4));
            Assert.That(taken[0].SourcePosition, Is.EqualTo(_alphaSpawn));
            Assert.That(_transport.Read<ShotMessage>(1, m => m), Is.Empty, "the shooter shows its own shot without waiting");
            Assert.That(_transport.Read<ShotMessage>(2, m => new ShotMessage { ImpactIsCharacter = m.ImpactIsCharacter }).All(s => s.ImpactIsCharacter), Is.True);
        }

        [Test]
        public void TheShotIsJudgedAgainstWhereTheTargetWasWhenTheShooterSawIt()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;
            var seen = Body(2).Center;

            Walk(2, new Vector2(0f, 1f), 11, InputButtons.Sprint);
            Assert.That(Vector3.Distance(Body(2).Center, seen), Is.GreaterThan(1f), "the target must have left the spot");

            FireAt(1, seen, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=11.00"));
        }

        [Test]
        public void AShotAtWhereTheTargetIsNowMissesWhenTheShooterSawAnOlderTick()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;

            Walk(2, new Vector2(0f, 1f), 11, InputButtons.Sprint);
            FireAt(1, Body(2).Center, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void RewindingStopsAtTheLimit()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;
            var seen = Body(2).Center;

            Walk(2, new Vector2(0f, 1f), LagCompensator.DefaultMaxRewindTicks + 30);
            FireAt(1, seen, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
        }

        [Test]
        public void AWallStopsTheBullet()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            var shot = _transport.Read<ShotMessage>(2, m => new ShotMessage { HasImpact = m.HasImpact, ImpactIsCharacter = m.ImpactIsCharacter, ImpactPoint = m.ImpactPoint }).Single();
            Assert.That(shot.HasImpact, Is.True);
            Assert.That(shot.ImpactIsCharacter, Is.False);
            Assert.That(shot.ImpactPoint.X, Is.EqualTo(-0.25f).Within(1e-3f));
        }

        [Test]
        public void ABrokenWallLetsTheNextBulletThrough()
        {
            Start(Rules().WithDestruction(Destruction.On), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Destruction] structure=0 destroyed=0 collapsed=-"));
            var state = _transport.Read<StructureStateMessage>(2, m => m).Single();
            Assert.That(state.StructureId, Is.EqualTo(0));

            Step(0.2f);
            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25));
        }

        [Test]
        public void ALatePlayerIsToldWhichPiecesAreBroken()
        {
            Start(Rules().WithDestruction(Destruction.On), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center);

            Join(3);

            Assert.That(_transport.Read<StructureStateMessage>(3, m => m), Has.Count.EqualTo(1));
        }

        [Test]
        public void WithoutDestructionTheWallStaysAndIsNotSent()
        {
            Start(Rules(), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_transport.Read<StructureStateMessage>(2, m => m), Is.Empty);
            Assert.That(_context.Map.Collision.IsGroupEnabled(MapColliderGroups.Piece(0, 0)), Is.True);
        }

        [Test]
        public void ShotsFromBehindAWallAreRejected()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, new Vector3(-1.5f, _alphaSpawn.Y, 0f), 0f, _host.TickCount);
            var origin = Body(1).EyePosition + new Vector3(2f, 0f, 0f);
            Fire(1, origin, Body(2).Center - origin, (int)_host.TickCount);

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=1 reason=OriginBlocked"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void ShotsUseTheMagazineAndAnEmptyOneReloads()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 30; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
                Step(0.1f);
            }

            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            Assert.That(_log.ToString(), Does.Contain("[Net] reload start client=1 auto=True ammo=0 reserve=90"));
            Step(2.1f);
            Assert.That(_log.ToString(), Does.Contain("[Net] reloaded client=1 ammo=30 reserve=60"));
        }

        [Test]
        public void TooFastShotsAreRejectedWithoutUsingAmmo()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < ShotBudget.DefaultBurst + 2; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            }

            Assert.That(_log.ToString(), Does.Contain("reason=FireRateExceeded"));
            var snapshot = Snapshot(1);
            Assert.That(snapshot.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30 - ShotBudget.DefaultBurst));
        }

        [Test]
        public void TheOwnerSnapshotCarriesTheWeaponsAndGrenades()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            var snapshot = Snapshot(1);

            Assert.That(snapshot.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(snapshot.Owner.Weapon.WeaponCount, Is.EqualTo(2));
            Assert.That(snapshot.Owner.EquippedWeaponIds[0], Is.EqualTo(TestRules.RifleId));
            Assert.That(snapshot.Owner.Grenades.Enabled, Is.True);
            Assert.That(snapshot.Owner.Grenades.Remaining, Is.EqualTo(2));
            Assert.That(snapshot.Players[0].WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void SwitchingWeaponsChangesWhatTheServerAcceptsAfterTheSwitchTime()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Fire(1, Body(1).EyePosition, Body(2).Center - Body(1).EyePosition, (int)_host.TickCount, TestRules.HandgunId);
            Assert.That(_log.ToString(), Does.Contain("reason=Switching"));

            Step(0.5f);
            Fire(1, Body(1).EyePosition, Body(2).Center - Body(1).EyePosition, (int)_host.TickCount, TestRules.HandgunId);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 34));
        }

        [Test]
        public void AMismatchedShotIndexIsCorrected()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            _shotIndex = 5;

            FireAt(1, Body(2).Center);

            var corrected = _transport.Read<ShotIndexCorrectedMessage>(
                1, m => new ShotIndexCorrectedMessage { Generation = m.Generation, ClientIndex = m.ClientIndex, ServerIndex = m.ServerIndex }).Single();
            Assert.That(corrected.ClientIndex, Is.EqualTo(5));
            Assert.That(corrected.ServerIndex, Is.EqualTo(0));
            Assert.That(corrected.Generation, Is.EqualTo(1));
        }

        [TestCase(0.5f)]
        [TestCase(2.5f)]
        [TestCase(4f)]
        [TestCase(5.5f)]
        public void TheExplosionDamageFallsOffWithDistance(float distance)
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var center = victim.Position + new Vector3(-(victim.Radius + distance), 0f, 0f);

            _combat.Explode(center, 1);

            var expected = GrenadeSpec.Default.Explosion.At(distance, false);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(Math.Max(0, NetMatchSettings.DefaultMaxHealth - expected)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net exploded thrower=1"));
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheExplosionDoesNotReachBeyondItsRadius()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);

            _combat.Explode(victim.Position + new Vector3(-(victim.Radius + GrenadeSpec.DefaultDamageRadius + 0.5f), 0f, 0f), 1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AWallBetweenTheExplosionAndTheBodyBlocksIt()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var center = new Vector3(-1f, victim.Position.Y, 0f);
            var distance = ServerCombat.DistanceToCapsule(
                center, victim.Position + new Vector3(0f, victim.Shape.BottomY, 0f), victim.Position + new Vector3(0f, victim.Shape.TopY, 0f), victim.Radius);

            _combat.Explode(center, 1);

            Assert.That(GrenadeSpec.Default.Explosion.At(distance, false), Is.GreaterThan(0), "without the wall it would hurt");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net blocked victim=2"));
        }

        [Test]
        public void AThrownGrenadeFliesBouncesAndExplodesAfterItsFuse()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = new Vector3(1f, 0f, 0f) });
            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net thrown player=1 remaining=1"));
            Step(0.5f);
            var snapshot = Snapshot(2);
            Assert.That(snapshot.Grenades.Count, Is.EqualTo(1));
            Assert.That(snapshot.Grenades[0].Position.X, Is.GreaterThan(_alphaSpawn.X + 2f));

            Step(GrenadeSpec.DefaultFuseSeconds);

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net exploded thrower=1"));
            Assert.That(
                _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center }).Single().Center.Y,
                Is.EqualTo(0.5f + ServerGrenadeFlight.BodyRadius).Within(0.01f),
                "the grenade should roll on the floor");
        }

        [Test]
        public void GrenadesAreLimitedByCountAndInterval()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var throwRequest = new ThrowGrenadeRequestMessage { Direction = new Vector3(0f, 1f, 1f) };

            _host.OnMessage(new ConnectionId(1), throwRequest);
            _host.OnMessage(new ConnectionId(1), throwRequest);
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net rejected player=1 reason=TooSoon"));
            Step(1f);
            _host.OnMessage(new ConnectionId(1), throwRequest);
            Step(1f);
            _host.OnMessage(new ConnectionId(1), throwRequest);

            Assert.That(Lines(_log).Count(line => line.StartsWith("[Grenade] net thrown", StringComparison.Ordinal)), Is.EqualTo(2));
        }

        [Test]
        public void GrenadesAreOffWhenTheRulesSaySo()
        {
            Start(Rules().WithActions(Rules().Actions.WithGrenade(Grenade.Off)));
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("reason=Disabled"));
        }

        [Test]
        public void AHardLandingHurtsWhenTheRuleIsOn()
        {
            Start(Rules().WithFallDamage(FallDamage.On));
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, _alphaSpawn + new Vector3(0f, 30f, 0f), 0f, _host.TickCount);
            Walk(1, Vector2.Zero, 180);

            var health = _rules.Session.GetHealth(1);
            Assert.That(health, Is.LessThan(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] fall owner=1 tick="));
            Assert.That(_log.ToString(), Does.Contain("damage=" + (NetMatchSettings.DefaultMaxHealth - health).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void AHardLandingDoesNotHurtWhenTheRuleIsOff()
        {
            Start(Rules().WithFallDamage(FallDamage.Off));
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, _alphaSpawn + new Vector3(0f, 30f, 0f), 0f, _host.TickCount);
            Walk(1, Vector2.Zero, 180);

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] fall owner="));
        }

        [Test]
        public void DeadPlayersCannotShoot()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ApplyDamage(2, 1, 1000);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithGrenade(Grenade.On).WithGrenadeCount(2);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(GameRules rules, bool wall = false, bool destructibleWall = false)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            NetMapStructures structures = null;
            if (wall)
            {
                var group = destructibleWall ? MapColliderGroups.Piece(0, 0) : WorldCollider.StaticGroup;
                colliders.Add(WorldCollider.Box(2, 0, group, new Vector3(0f, 2.5f, 0f), new Vector3(0.25f, 2f, 3f), Quaternion.Identity));
                if (destructibleWall)
                {
                    structures = new NetMapStructures(new[] { new StructureLayout(_wallHealth, _wallGrounded, Array.Empty<(int, int)>()) });
                }
            }

            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, structures);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, _movement);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void FireAt(int shooter, Vector3 target, int? seenTick = null)
        {
            var eye = Body(shooter).EyePosition;
            Fire(shooter, eye, target - eye, seenTick ?? (int)_host.TickCount);
        }

        private void Fire(int shooter, Vector3 origin, Vector3 direction, int tick, string weaponId = TestRules.RifleId)
        {
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = weaponId,
                Origin = origin,
                Direction = direction,
                ClientTick = tick,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
            });
        }

        private void Walk(int id, Vector2 move, int ticks, InputButtons buttons = InputButtons.None)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, move, 0f, buttons));
                _host.OnMessage(new ConnectionId((ulong)id), input);
                Tick();
            }
        }

        private SnapshotMessage Snapshot(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _combat.Contribute(snapshot, receiver);
            return snapshot;
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

        private static KilledMessage Copy(KilledMessage m)
        {
            return new KilledMessage { KillerId = m.KillerId, VictimId = m.VictimId, WeaponId = m.WeaponId, Headshot = m.Headshot, Friendly = m.Friendly };
        }

        private static IEnumerable<string> Lines(StringWriter writer)
        {
            return writer.ToString().Split('\n').Select(line => line.TrimEnd('\r'));
        }
    }
}
