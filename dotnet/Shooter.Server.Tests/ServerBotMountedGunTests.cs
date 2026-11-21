using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // bots.json の mountedGun を書いた Bot が、オフラインの EnemyMountedGunner と同じく固定銃座に乗り、座席の武器で撃ち、撃てなくなったら降りる
    public sealed class ServerBotMountedGunTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _gunId = "5b0e2c7a-9d41-4f3e-8a62-1c7d9e4b3f05";
        private const int _magazine = 100;
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 床の上に +Z を向けて置いた銃座。乗る点は後ろ 1.2m、降りる点は右 1.5m。左右 60° ずつしか回らない
        private static readonly Vector3 _gunPosition = Vector3.Zero;
        private static readonly Vector3 _enterLocal = new Vector3(0f, 0f, -1.2f);
        private static readonly Vector3 _exitLocal = new Vector3(1.5f, 0f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerVehicles _vehicles;
        private ServerCombat _combat;
        private ServerBots _bots;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
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
        public void ARiflemanWithTheItemSeeksGuns()
        {
            var agent = Agent("Rifleman", "\"mountedGun\": {}");

            Assert.That(agent.MansGuns, Is.True);
            Assert.That(agent.Brain.Behaviors.Single(), Is.SameAs(agent.GunnerBehavior));
            Assert.That(agent.GunnerBehavior.Spec.SeekRadius, Is.EqualTo(MountedGunUseSpec.DefaultSeekRadius));
            Assert.That(agent.MannedGun, Is.EqualTo(-1));
            Assert.That(agent.GunCandidate, Is.EqualTo(-1));
        }

        [Test]
        public void WithoutTheItemABotDoesNotSeekGuns()
        {
            var agent = Agent("Rifleman");

            Assert.That(agent.MansGuns, Is.False);
            Assert.That(agent.GunnerBehavior, Is.Null);
        }

        [TestCase("Shield")]
        [TestCase("Sniper")]
        [TestCase("Charger")]
        [TestCase("Bomber")]
        public void OnlyARiflemanSeeksGuns(string archetype)
        {
            var agent = Agent(archetype, "\"mountedGun\": {}");

            Assert.That(agent.MansGuns, Is.False);
            Assert.That(agent.Brain.Behaviors.OfType<MountedGunEnemyBehavior>(), Is.Empty);
        }

        [Test]
        public void AGunnerTurnsTheGunAtItsTurnSpeedAndFiresOnceItPoints()
        {
            var agent = Agent("Rifleman", "\"mountedGun\": {}");
            agent.StartManning(3, new VehicleWeaponSpec(_gunId, 0, -90f, 90f, turnSpeedDegreesPerSecond: 90f));
            var right = new Vector3(1f, 0f, 1f);

            var first = agent.Man(0.1f, true, true, right);

            Assert.That(agent.MannedGun, Is.EqualTo(3));
            Assert.That(first.Fire, Is.False, "45 degrees away takes half a second to turn");
            Assert.That(first.Leave, Is.Null);

            var order = first;
            for (var i = 0; i < 5; i++)
            {
                order = agent.Man(0.1f, true, true, right);
            }

            Assert.That(order.Fire, Is.True);
            Assert.That(Vector3.Dot(agent.GunLocalDirection, Vector3.Normalize(right)), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void AGunnerThatLosesItsTargetLeavesAfterTheGiveUpSeconds()
        {
            var agent = Agent("Rifleman", "\"mountedGun\": {\"giveUpSeconds\": 1, \"remountDelaySeconds\": 4}");
            agent.StartManning(0, new VehicleWeaponSpec(_gunId, 0));

            Assert.That(agent.Man(0.5f, false, false, null).Leave, Is.Null);
            var order = agent.Man(0.5f, false, false, null);

            Assert.That(order.Leave, Is.EqualTo(MountedGunLeaveCause.TargetLost));
            Assert.That(order.Fire, Is.False);

            agent.NoteLeftGun();

            Assert.That(agent.MannedGun, Is.EqualTo(-1));
            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void AGunnerThatCannotTurnToItsTargetLeavesOutOfArc()
        {
            var agent = Agent("Rifleman", "\"mountedGun\": {\"giveUpSeconds\": 0.5}");
            agent.StartManning(0, new VehicleWeaponSpec(_gunId, 0));

            var order = agent.Man(0.6f, true, true, null);

            Assert.That(order.Leave, Is.EqualTo(MountedGunLeaveCause.OutOfArc));
        }

        [Test]
        public void ARespawnedGunnerForgetsItsGunAndItsWait()
        {
            var agent = Agent("Rifleman", "\"mountedGun\": {\"remountDelaySeconds\": 4}");
            agent.StartManning(0, new VehicleWeaponSpec(_gunId, 0));
            agent.NoteLeftGun();
            agent.SetGunCandidate(2, new Vector3(1f, 0f, 1f));
            agent.MarkDead();

            agent.Restart(true);

            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.Zero);
            Assert.That(agent.GunnerBehavior.Candidate, Is.Null);
            Assert.That(agent.GunCandidate, Is.EqualTo(-1));
            Assert.That(agent.MannedGun, Is.EqualTo(-1));
        }

        [Test]
        public void ManningWithoutAGunIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Agent("Rifleman", "\"mountedGun\": {}").Man(0.1f, true, true, Vector3.UnitZ));
            Assert.Throws<InvalidOperationException>(() => Agent("Rifleman").Man(0.1f, true, true, Vector3.UnitZ));
        }

        [Test]
        public void ABotFindsTheFreeGunThatCanAimAtItsTarget()
        {
            Start("{\"mountedGun\": {}}", 0);

            Assert.That(_vehicles.IsMountedGun(0), Is.True);
            Assert.That(_vehicles.TryFindMountedGun(new Vector3(0f, 0f, -3f), new Vector3(0f, 1f, 15f), out var vehicle, out var board), Is.True);
            Assert.That(vehicle, Is.Zero);
            Assert.That(Vector3.Distance(board, _gunPosition + _enterLocal), Is.LessThan(1e-4f));

            // 銃座は前の左右 60° しか回らないので、後ろの相手へは向けられない
            Assert.That(_vehicles.TryFindMountedGun(new Vector3(0f, 0f, -3f), new Vector3(0f, 1f, -15f), out _, out _), Is.False);
        }

        [Test]
        public void AVehicleWithADriverSeatIsNotAMountedGun()
        {
            Start("{\"mountedGun\": {}}", 0, Jeep());

            Assert.That(_vehicles.IsMountedGun(0), Is.False);
            Assert.That(_vehicles.TryFindMountedGun(Vector3.Zero, new Vector3(0f, 1f, 15f), out _, out _), Is.False);
        }

        [Test]
        public void ABotMansTheGunFiresItsWeaponAndLeavesWhenItCannotTurnToTheTarget()
        {
            Start("{\"mountedGun\": {\"giveUpSeconds\": 0.5, \"remountDelaySeconds\": 30}}", 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Teleport(botId, new Vector3(0f, 1.1f, -1.8f));
            Teleport(1, new Vector3(0f, 1.1f, 15f));

            for (var elapsed = 0f; elapsed < 5f && !_vehicles.TryGetSeat(botId, out _, out _); elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_vehicles.TryGetSeat(botId, out var vehicle, out var seat), Is.True, _log.ToString());
            Assert.That((vehicle, seat), Is.EqualTo((0, 0)));
            Assert.That(_log.ToString(), Does.Contain("[Bot] gunner player=" + Text(botId) + " man gun=turret#0"));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'turret#0' board rider=" + Text(botId) + " seat=0 role=Gunner"));
            Assert.That(Row(Snapshot(1), botId).Vehicle.SameAs(new VehicleSeatSync(0, 0)), Is.True);

            WeaponSync state = default;
            for (var elapsed = 0f; elapsed < 3f; elapsed += _tickSeconds)
            {
                Tick();
                Assert.That(_combat.TryGetBotWeapon(botId, out var spec, out state), Is.True);
                Assert.That(spec.Id, Is.EqualTo(_gunId), "a gunner fires the seat's weapon, not the one it carries");
                if (state.AmmoInMagazine < _magazine)
                {
                    break;
                }
            }

            Assert.That(state.AmmoInMagazine, Is.LessThan(_magazine), _log.ToString());
            Assert.That(_log.ToString(), Does.Not.Contain("rejected shot attacker=" + Text(botId)));

            // 後ろへ回った相手には銃座を向けられないので、あきらめる秒数の後に降りる
            Teleport(1, new Vector3(0f, 1.1f, -15f));
            for (var elapsed = 0f; elapsed < 2f && _vehicles.TryGetSeat(botId, out _, out _); elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.False, _log.ToString());
            Assert.That(_movement.IsRiding(botId), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Bot] gunner player=" + Text(botId) + " leave gun=turret#0 cause=OutOfArc"));
            Assert.That(_bots.TryGetAgent(botId, out var agent), Is.True);
            Assert.That(agent.MannedGun, Is.EqualTo(-1));
            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.GreaterThan(0f));

            // 降りる位置への置き直しは出現し直しではないので、数 tick 進めても待ち時間が消えない
            for (var i = 0; i < 5; i++)
            {
                Tick();
            }

            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void ABotWithoutTheItemWalksPastTheGun()
        {
            Start("{}", 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Teleport(botId, new Vector3(0f, 1.1f, -1.8f));
            Teleport(1, new Vector3(0f, 1.1f, 15f));

            Step(3f);

            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Bot] gunner"));
        }

        [Test]
        public void ABotThatDiesOnTheGunFreesTheSeat()
        {
            Start("{\"mountedGun\": {\"remountDelaySeconds\": 30}}", 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Teleport(botId, new Vector3(0f, 1.1f, -1.8f));
            Teleport(1, new Vector3(0f, 1.1f, 15f));
            for (var elapsed = 0f; elapsed < 5f && !_vehicles.TryGetSeat(botId, out _, out _); elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.True, _log.ToString());

            _rules.ApplyDamage(null, botId, 1000);
            Tick();
            Tick();

            Assert.That(_vehicles.OccupantOf(0, 0), Is.EqualTo(PlayerIds.None));
            Assert.That(_movement.IsRiding(botId), Is.False);
        }

        [Test]
        public void ABotThatSurvivesTheGunBeingWreckedKeepsItsRemountWait()
        {
            Start("{\"mountedGun\": {\"remountDelaySeconds\": 30}}", 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Teleport(botId, new Vector3(0f, 1.1f, -1.8f));
            Teleport(1, new Vector3(0f, 1.1f, 15f));
            for (var elapsed = 0f; elapsed < 5f && !_vehicles.TryGetSeat(botId, out _, out _); elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.True, _log.ToString());

            _vehicles.Damage(0, 100000, 1);
            Step(5 * _tickSeconds);

            Assert.That(_bots.TryGetAgent(botId, out var agent), Is.True);
            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.False);
            Assert.That(agent.MannedGun, Is.EqualTo(-1));
            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.GreaterThan(0f), "a wrecked gun is not a respawn");
            Assert.That(_log.ToString(), Does.Contain("[Bot] gunner player=" + Text(botId) + " lost gun="));
        }

        [Test]
        public void ABotRespawnedByTheRulesWhileOnTheGunForgetsItsRemountWait()
        {
            Start("{\"mountedGun\": {\"remountDelaySeconds\": 30}}", 2);
            Join(1);
            Tick();
            var botId = _bots.BotIds.Single();
            Teleport(botId, new Vector3(0f, 1.1f, -1.8f));
            Teleport(1, new Vector3(0f, 1.1f, 15f));
            for (var elapsed = 0f; elapsed < 5f && !_vehicles.TryGetSeat(botId, out _, out _); elapsed += _tickSeconds)
            {
                Tick();
            }

            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.True, _log.ToString());

            // ラウンドの頭の出現し直しと同じく、乗り物の外から置き直す
            Teleport(botId, new Vector3(5f, 1.1f, 5f));
            Tick();
            Tick();

            Assert.That(_bots.TryGetAgent(botId, out var agent), Is.True);
            Assert.That(_vehicles.TryGetSeat(botId, out _, out _), Is.False);
            Assert.That(agent.GunnerBehavior.RemountRemainingSeconds, Is.EqualTo(0f), "a respawn clears the remount wait");
        }

        private static ServerBotAgent Agent(string archetype, string extra = null)
        {
            var json = "{\"archetype\": \"" + archetype + "\"" + (extra != null ? ", " + extra : string.Empty) + "}";
            var spec = TuningJson.ParseBots(json, "bots.json");
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f)));
            var navigation = new ServerBotNavigation(
                map.Navigation, map.NavMesh.Links, map.Ladders.Ladders, map.World, _ => false, spec.NavSampleRadius, 1f);
            var agent = new ServerBotAgent(1, spec, navigation, Array.Empty<Vector3>(), new Random(1), new SeeEverything());
            agent.Restart(true);
            return agent;
        }

        private static MapVehicle Turret()
        {
            var gun = new WeaponSpec(_gunId, "weapon.mounted", WeaponCategory.AssaultRifle, FireMode.Auto, _magazine, 0, 0.1f, 2f, 5, 100f, DamageFalloff.None);
            var spec = new VehicleSpec(
                "turret",
                500,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing) },
                new[] { new VehicleWeaponSpec(_gunId, 0, -60f, 60f, -20f, 30f, 180f) });
            return new MapVehicle(
                spec,
                _gunPosition,
                Quaternion.Identity,
                new[] { new MapVehicleSeat(new Vector3(0f, 0f, -0.6f), 0f, new[] { _enterLocal }, new[] { _exitLocal }) },
                null,
                null,
                new[] { gun });
        }

        private static MapVehicle Jeep()
        {
            var spec = new VehicleSpec(
                "jeep",
                500,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver), new VehicleSeatSpec(VehicleSeatRole.Gunner) },
                new[] { new VehicleWeaponSpec(_gunId, 1) });
            var gun = new WeaponSpec(_gunId, "weapon.mounted", WeaponCategory.AssaultRifle, FireMode.Auto, _magazine, 0, 0.1f, 2f, 5, 100f, DamageFalloff.None);
            return new MapVehicle(
                spec,
                _gunPosition,
                Quaternion.Identity,
                new[] { new MapVehicleSeat(new Vector3(-0.5f, 0.5f, 0f)), new MapVehicleSeat(new Vector3(0f, 1f, -1f)) },
                null,
                null,
                new[] { gun });
        }

        private void Start(string botsJson, int bots, MapVehicle vehicle = null)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var spec = TuningJson.ParseBots(botsJson, "bots.json");
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null, bots: spec);
            var points = BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f));
            var map = BotTestMaps.Flat(points, vehicles: new NetMapVehicles(new[] { vehicle ?? Turret() }));
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, bots, 0, 0, 0, 0);
            _context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), new StubControl(), data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _vehicles = new ServerVehicles(_rules, _movement);
            _combat = new ServerCombat(_rules, _movement, _vehicles);
            _bots = new ServerBots(_rules, _movement, _combat, vehicles: _vehicles);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_vehicles);
            _host.Register(_combat);
            _host.Register(_bots);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Teleport(int id, Vector3 origin)
        {
            _context.Bodies.Teleport(id, origin, 0f, _host.TickCount);
        }

        private SnapshotMessage Snapshot(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            snapshot.HasOwner = true;
            _vehicles.Contribute(snapshot, receiver);
            _bots.Contribute(snapshot, receiver);
            return snapshot;
        }

        private static PlayerSnapshot Row(SnapshotMessage snapshot, int playerId)
        {
            return snapshot.Players[snapshot.Players.IndexOf(playerId)];
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

        private static string Text(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private sealed class SeeEverything : ISightBlocker
        {
            public bool Blocks(Vector3 from, Vector3 to) => false;
        }
    }
}
