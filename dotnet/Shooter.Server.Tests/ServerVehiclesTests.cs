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
    // 移動・ルール・乗り物・戦闘を同じホストで動かし、乗り降り・運転の確かめ・座席からの射撃・耐久と爆発を確かめる
    public sealed class ServerVehiclesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _gunId = "c3d1a0e5-6f2b-4b7d-9e1a-5a8c4d2f7b90";
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly int[] _bodyThenEngine = { 25, 50 };

        // 床の上 (y=0.5) に置いた車。運転席は左、助手席は右で右を向き、銃座は後ろに立つ
        private static readonly Vector3 _jeepPosition = new Vector3(0f, 0.5f, 0f);
        private static readonly Vector3 _driverEnter = new Vector3(-1.5f, 0f, 0.3f);
        private static readonly Vector3 _driverExit = new Vector3(-2f, 0f, 0.3f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerMovement _movement;
        private ServerVehicles _vehicles;
        private ServerCombat _combat;
        private int _shotIndex;
        private int _sequence;
        private int _inputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _shotIndex = 0;
            _sequence = 0;
            _inputTick = 0;
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
        public void BoardingTakesTheNearestFreeSeatAndEveryoneSeesIt()
        {
            StartWithTwoPlayers();
            StandAt(1, _driverEnter);

            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 0, VehicleRequestMessage.AnySeat), Is.True, _log.ToString());
            Tick();

            Assert.That(_vehicles.TryGetSeat(1, out var vehicle, out var seat), Is.True);
            Assert.That((vehicle, seat), Is.EqualTo((0, 0)));
            Assert.That(_movement.IsRiding(1), Is.True);
            var expected = MapVehicle.RiderOrigin(_jeepPosition + new Vector3(-0.5f, 0.5f, 0.3f), _context.Bodies.Geometry, crouched: true);
            Assert.That(Vector3.Distance(Body(1).Position, expected), Is.LessThan(1e-4f));
            Assert.That(Body(1).Stance, Is.EqualTo(PlayerStance.Crouching), "a seated rider is as tall as a crouching one");
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' board rider=1 seat=0 role=Driver"));
            var entered = Lines().Single(line => line.Contains("\"type\":\"vehicle_entered\"", StringComparison.Ordinal));
            Assert.That(entered, Does.Contain("\"playerId\":\"1\"").And.Contain("\"vehicleId\":\"jeep\"").And.Contain("\"role\":\"Driver\""));

            var snapshot = Snapshot(2);
            Assert.That(snapshot.Vehicles.Count, Is.EqualTo(1));
            Assert.That(snapshot.Vehicles[0].Health, Is.EqualTo(1000));
            Assert.That(snapshot.Vehicles[0].Stage, Is.EqualTo(VehicleDamageStage.Intact));
            Assert.That(Row(snapshot, 1).Vehicle.SameAs(new VehicleSeatSync(0, 0)), Is.True);
            Assert.That(Row(snapshot, 2).Vehicle.IsRiding, Is.False);
        }

        [Test]
        public void TheNewDriverIsGivenThePoseToDriveFrom()
        {
            StartWithTwoPlayers();
            StandAt(1, _driverEnter);

            _vehicles.Handle(1, VehicleAction.Board, 0, 0);

            var corrected = Corrections(1).Single();
            Assert.That(corrected.Generation, Is.EqualTo(1));
            Assert.That(corrected.Position, Is.EqualTo(_jeepPosition));
        }

        [TestCase(0, "out_of_range")]
        [TestCase(5, "seat_removed")]
        public void BoardingIsRefusedOutOfRangeOrWithoutTheSeat(int seat, string reason)
        {
            StartWithTwoPlayers();

            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 0, seat), Is.False);
            Assert.That(_vehicles.TryGetSeat(1, out _, out _), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' board refused rider=1 reason=" + reason));
        }

        [Test]
        public void ATakenSeatIsRefusedButAnotherFreeSeatIsChosen()
        {
            StartWithTwoPlayers();
            StandAt(1, _driverEnter);
            StandAt(2, _driverEnter);
            _vehicles.Handle(1, VehicleAction.Board, 0, 0);

            Assert.That(_vehicles.Handle(2, VehicleAction.Board, 0, 0), Is.False);
            Assert.That(_log.ToString(), Does.Contain("board refused rider=2 reason=seat_taken"));
            Assert.That(_vehicles.Handle(2, VehicleAction.Board, 5, VehicleRequestMessage.AnySeat), Is.False);
            Assert.That(_log.ToString(), Does.Contain("board refused rider=2 reason=unknown_vehicle"));
        }

        [Test]
        public void LeavingPutsTheRiderAtTheExitAndTellsEveryone()
        {
            StartWithTwoPlayers();
            StandAt(1, _driverEnter);
            _vehicles.Handle(1, VehicleAction.Board, 0, 0);
            Tick();

            Assert.That(_vehicles.Handle(1, VehicleAction.Leave, 0, VehicleRequestMessage.AnySeat), Is.True, _log.ToString());

            var expected = MapVehicle.RiderOrigin(_jeepPosition + _driverExit, _context.Bodies.Geometry, crouched: false);
            Assert.That(Vector3.Distance(Body(1).Position, expected), Is.LessThan(1e-4f));
            Assert.That(_movement.IsRiding(1), Is.False);
            Assert.That(_vehicles.TryGetSeat(1, out _, out _), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' leave rider=1 seat=0 reason=Request exit=0"));
            var left = Lines().Single(line => line.Contains("\"type\":\"vehicle_left\"", StringComparison.Ordinal));
            Assert.That(left, Does.Contain("\"reason\":\"Request\""));
            var teleported = _transport.Read<TeleportedMessage>(2, m => new TeleportedMessage { PlayerId = m.PlayerId, Position = m.Position });
            Assert.That(teleported.Last().PlayerId, Is.EqualTo(1));
            Assert.That(teleported.Last().Position, Is.EqualTo(Body(1).Position));
        }

        [Test]
        public void ARiderCannotLeaveWhenEveryExitIsBlocked()
        {
            StartWithTwoPlayers(blockExits: true);
            StandAt(1, _driverEnter);
            _vehicles.Handle(1, VehicleAction.Board, 0, 0);

            Assert.That(_vehicles.Handle(1, VehicleAction.Leave, 0, VehicleRequestMessage.AnySeat), Is.False);
            Assert.That(_vehicles.TryGetSeat(1, out _, out _), Is.True);
            Assert.That(_log.ToString(), Does.Contain("leave refused rider=1 reason=exit_blocked"));
        }

        [Test]
        public void SwitchingGoesToTheNextFreeSeat()
        {
            StartWithTwoPlayers();
            StandAt(1, _driverEnter);
            _vehicles.Handle(1, VehicleAction.Board, 0, 0);

            Assert.That(_vehicles.Handle(1, VehicleAction.SwitchSeat, 0, VehicleRequestMessage.AnySeat), Is.True);

            Assert.That(_vehicles.TryGetSeat(1, out _, out var seat), Is.True);
            Assert.That(seat, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' switch rider=1 from=0 to=1 role=Passenger"));
        }

        [Test]
        public void TheDriversPredictedPoseIsAcceptedAndCarriesTheRiders()
        {
            StartWithTwoPlayers();
            BoardDriver();
            var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f);

            Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 2f), turned);
            Tick();

            Assert.That(_vehicles.TryGetState(0, out var position, out var rotation, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition + new Vector3(0f, 0f, 2f)));
            Assert.That(Quaternion.Dot(rotation, turned), Is.EqualTo(1f).Within(1e-5f));
            var seatFeet = MapVehicle.ToWorld(new Vector3(-0.5f, 0.5f, 0.3f), position, rotation);
            var expected = MapVehicle.RiderOrigin(seatFeet, _context.Bodies.Geometry, crouched: true);
            Assert.That(Vector3.Distance(Body(1).Position, expected), Is.LessThan(1e-4f));
            Assert.That(Corrections(1), Has.Count.EqualTo(1), "only the hand-over");
        }

        [Test]
        public void ADriverThatJumpsTooFarIsCorrected()
        {
            StartWithTwoPlayers();
            BoardDriver();

            Drive(1, generation: 1, _jeepPosition + new Vector3(50f, 0f, 0f), Quaternion.Identity);

            Assert.That(_vehicles.TryGetState(0, out var position, out _, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition));
            var corrections = Corrections(1);
            Assert.That(corrections, Has.Count.EqualTo(2));
            Assert.That(corrections[1].Generation, Is.EqualTo(2));
            Assert.That(corrections[1].Position, Is.EqualTo(_jeepPosition));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' corrected driver=1 reason=TooFast generation=2"));

            // 直す知らせを受け取る前に送った姿は、もう一度直さずに捨てる
            Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 60f), Quaternion.Identity);
            Assert.That(Corrections(1), Has.Count.EqualTo(2));

            Drive(1, generation: 2, _jeepPosition + new Vector3(0f, 0f, 1f), Quaternion.Identity);
            Assert.That(_vehicles.TryGetState(0, out position, out _, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition + new Vector3(0f, 0f, 1f)));
        }

        [Test]
        public void DrivingThroughAWallIsCorrected()
        {
            StartWithTwoPlayers();
            BoardDriver();

            Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 10f), Quaternion.Identity);

            Assert.That(_vehicles.TryGetState(0, out var position, out _, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition));
            Assert.That(_log.ToString(), Does.Contain("corrected driver=1 reason=Blocked"));
        }

        [Test]
        public void AnOlderPoseThanTheLastIsIgnored()
        {
            StartWithTwoPlayers();
            BoardDriver();
            Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 1f), Quaternion.Identity);

            _sequence = 0;
            Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 0.5f), Quaternion.Identity);

            Assert.That(_vehicles.TryGetState(0, out var position, out _, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition + new Vector3(0f, 0f, 1f)));
        }

        [Test]
        public void OnlyTheDriverMovesTheVehicle()
        {
            StartWithTwoPlayers();
            StandAt(2, new Vector3(1.5f, 0f, 0.3f));
            _vehicles.Handle(2, VehicleAction.Board, 0, 1);

            Drive(2, generation: 0, _jeepPosition + new Vector3(0f, 0f, 1f), Quaternion.Identity);

            Assert.That(_vehicles.TryGetState(0, out var position, out _, out _, out _), Is.True);
            Assert.That(position, Is.EqualTo(_jeepPosition));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' move dropped rider=2 reason=NotDriver"));
        }

        [Test]
        public void ShotsDamageTheVehicleAndThePartMultiplies()
        {
            StartWithTwoPlayers();

            // 車体の箱 (倍率 1) と、上に載ったエンジンの箱 (倍率 2) を横から撃つ
            Fire(2, new Vector3(5f, 1f, 0f), -Vector3.UnitX, Tick0());
            Step(0.2f);
            Fire(2, new Vector3(5f, 1.5f, 1.5f), -Vector3.UnitX, Tick0());

            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out _), Is.True);
            Assert.That(health, Is.EqualTo(1000 - 25 - 50), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' damaged dmg=25 hp=975/1000"));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' damaged dmg=50 hp=925/1000"));
            var confirms = _transport.Read<HitConfirmMessage>(2, m => new HitConfirmMessage { Damage = m.Damage });
            Assert.That(confirms.Select(c => c.Damage), Is.EqualTo(_bodyThenEngine));
        }

        [Test]
        public void TheShotIsJudgedAgainstWhereTheVehicleWasWhenTheShooterSawIt()
        {
            StartWithTwoPlayers();
            BoardDriver();
            var seenTick = Tick0();
            for (var i = 1; i <= 10; i++)
            {
                Drive(1, generation: 1, _jeepPosition + new Vector3(0f, 0f, 0.3f * i), Quaternion.Identity);
                Tick();
            }

            Fire(2, new Vector3(5f, 1.5f, 1.5f), -Vector3.UnitX, seenTick);
            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out _), Is.True);
            Assert.That(health, Is.EqualTo(950), _log.ToString());

            Step(0.2f);
            Fire(2, new Vector3(5f, 1.5f, 1.5f), -Vector3.UnitX, Tick0());
            Assert.That(_vehicles.TryGetState(0, out _, out _, out health, out _), Is.True);
            Assert.That(health, Is.EqualTo(950), "the engine has moved on");
        }

        [Test]
        public void AVehicleThatLosesItsHealthExplodesTakesItsRiderAndStaysAWreck()
        {
            StartWithTwoPlayers(maxHealth: 50);
            BoardDriver();

            Fire(2, new Vector3(5f, 1f, 0f), -Vector3.UnitX, Tick0());
            Step(0.2f);
            Fire(2, new Vector3(5f, 1f, 0f), -Vector3.UnitX, Tick0());

            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out var stage), Is.True);
            Assert.That(health, Is.EqualTo(0));
            Assert.That(stage, Is.EqualTo(VehicleDamageStage.Destroyed));
            Assert.That(_rules.Session.IsAlive(1), Is.False, _log.ToString());
            Assert.That(_vehicles.TryGetSeat(1, out _, out _), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' exploded"));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' wrecked riders=1"));

            var exploded = Lines().Single(line => line.Contains("\"type\":\"vehicle_exploded\"", StringComparison.Ordinal));
            Assert.That(exploded, Does.Contain("\"attackerId\":\"2\"").And.Contain("\"riders\":\"1\"").And.Contain("\"killed\":\"1\""));
            var left = Lines().Single(line => line.Contains("\"type\":\"vehicle_left\"", StringComparison.Ordinal));
            Assert.That(left, Does.Contain("\"reason\":\"RiderDied\""));
            var kill = Lines().Single(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal));
            Assert.That(kill, Does.Contain("\"killerId\":\"2\"").And.Contain("\"victimId\":\"1\""));
            var killed = _transport.Read<KilledMessage>(1, m => new KilledMessage { WeaponId = m.WeaponId }).Single();
            Assert.That(killed.WeaponId, Is.EqualTo(VehicleSpec.ExplosionWeaponId));
            var explosion = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { WeaponId = m.WeaponId, HasThrower = m.HasThrower }).Single();
            Assert.That(explosion.WeaponId, Is.EqualTo(VehicleSpec.ExplosionWeaponId));
            Assert.That(Snapshot(2).Vehicles[0].Stage, Is.EqualTo(VehicleDamageStage.Destroyed));

            // 残骸はまだ弾を止めるが、もう傷まず、乗れない
            Step(0.2f);
            Fire(2, new Vector3(5f, 1f, 0f), -Vector3.UnitX, Tick0());
            Assert.That(_log.ToString().Split('\n').Count(line => line.Contains("damaged dmg=", StringComparison.Ordinal)), Is.EqualTo(2));
            StandAt(2, _driverEnter);
            Assert.That(_vehicles.Handle(2, VehicleAction.Board, 0, VehicleRequestMessage.AnySeat), Is.False);
            Assert.That(_log.ToString(), Does.Contain("board refused rider=2 reason=wrecked"));
        }

        [Test]
        public void ABurningVehicleKeepsLosingHealth()
        {
            StartWithTwoPlayers();

            _vehicles.Damage(0, 800, 2);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'jeep#0' stage=Burning hp=200/1000"));
            Step(1f);

            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out var stage), Is.True);
            Assert.That(health, Is.EqualTo(200 - (int)VehicleDurabilitySpec.DefaultBurnDamagePerSecond).Within(1));
            Assert.That(stage, Is.EqualTo(VehicleDamageStage.Burning));
        }

        [Test]
        public void ABlastNearTheVehicleDamagesItByDistance()
        {
            StartWithTwoPlayers();
            var explosion = new ExplosionSpec(new ExplosionDamage(100, 4f));

            _combat.Explode(new Vector3(3f, 1f, 0f), explosion, 2, "grenade");

            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out _), Is.True);
            Assert.That(health, Is.EqualTo(1000 - explosion.Damage.At(2f, false)));
            Assert.That(_log.ToString(), Does.Contain("vehicles=1"));
        }

        [Test]
        public void TheGunnerFiresTheMountedWeaponWithLagCompensation()
        {
            StartWithTwoPlayers();
            StandAt(1, new Vector3(0f, 0f, -2.5f));
            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 0, 2), Is.True, _log.ToString());
            Tick();
            var seenTick = Tick0();
            var seen = Body(2).Center;
            Walk(2, new Vector2(0f, 1f), 11);
            Assert.That(Vector3.Distance(Body(2).Center, seen), Is.GreaterThan(1f), "the target must have left the spot");

            FireFrom(1, seen, seenTick, _gunId);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            var owner = Snapshot(1).Owner;
            Assert.That(owner.EquippedWeaponIds.Count, Is.EqualTo(1));
            Assert.That(owner.EquippedWeaponIds[0], Is.EqualTo(_gunId));
            Assert.That(Row(Snapshot(2), 1).WeaponId, Is.EqualTo(_gunId));
        }

        [Test]
        public void TheGunnerCannotFireTheMountedWeaponOutsideItsTraverse()
        {
            StartWithTwoPlayers();
            StandAt(1, new Vector3(0f, 0f, -2.5f));
            _vehicles.Handle(1, VehicleAction.Board, 0, 2);
            Tick();

            // 銃座の上下の範囲は -45〜30 度なので、真上へは向けられない
            Fire(1, Body(1).EyePosition, Vector3.UnitY, Tick0(), _gunId);

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=1 reason=OutsideSeatArc"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] shot attacker=1"));
        }

        [Test]
        public void TheHullBlocksBlastsAndSprayButNotItsOwnRiders()
        {
            StartWithTwoPlayers();

            Assert.That(_vehicles.BlocksLine(new Vector3(0f, 1f, 5f), new Vector3(0f, 1f, -5f), float.MaxValue, PlayerIds.None), Is.True);
            Assert.That(_vehicles.BlocksLine(new Vector3(0f, 3f, 5f), new Vector3(0f, 3f, -5f), float.MaxValue, PlayerIds.None), Is.False);

            StandAt(1, _driverEnter);
            _vehicles.Handle(1, VehicleAction.Board, 0, 0);
            Assert.That(
                _vehicles.BlocksLine(new Vector3(0f, 1f, 5f), new Vector3(0f, 1f, -5f), float.MaxValue, 1),
                Is.False,
                "a rider's own vehicle does not block what the rider fires");
        }

        [Test]
        public void TheGunnerCannotFireTheirOwnWeapon()
        {
            StartWithTwoPlayers();
            StandAt(1, new Vector3(0f, 0f, -2.5f));
            _vehicles.Handle(1, VehicleAction.Board, 0, 2);
            Tick();

            FireFrom(1, Body(2).Center, Tick0(), TestRules.RifleId);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("reason=WeaponMismatch"));
        }

        [Test]
        public void TheDriverCannotFire()
        {
            StartWithTwoPlayers();
            BoardDriver();
            Tick();

            FireFrom(1, Body(2).Center, Tick0(), TestRules.RifleId);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=1 reason=SeatCannotFire"));
        }

        [Test]
        public void APassengerFiresTheirOwnWeaponOnlyWithinTheSeatArc()
        {
            StartWithTwoPlayers();
            StandAt(1, new Vector3(1.5f, 0f, 0.3f));
            _vehicles.Handle(1, VehicleAction.Board, 0, 1);
            Tick();

            // 助手席は右 (+X) を向き、前後 180 度の範囲で撃てる
            var eye = Body(1).EyePosition;
            Fire(1, eye, -Vector3.UnitX, Tick0(), TestRules.RifleId);
            Assert.That(_log.ToString(), Does.Contain("reason=OutsideSeatArc"));

            Step(0.2f);
            FireFrom(1, Body(2).Center, Tick0(), TestRules.RifleId);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
        }

        [Test]
        public void ARiderWhoDiesOrLeavesTheMatchFreesTheSeat()
        {
            StartWithTwoPlayers();
            BoardDriver();
            StandAt(2, new Vector3(1.5f, 0f, 0.3f));
            _vehicles.Handle(2, VehicleAction.Board, 0, 1);

            _rules.ApplyDamage(null, 1, 1000);
            Tick();
            _match.Leave(new ConnectionId(2));

            Assert.That(_vehicles.OccupantOf(0, 0), Is.EqualTo(PlayerIds.None));
            Assert.That(_vehicles.OccupantOf(0, 1), Is.EqualTo(PlayerIds.None));
            var reasons = Lines().Where(line => line.Contains("\"type\":\"vehicle_left\"", StringComparison.Ordinal)).ToList();
            Assert.That(reasons[0], Does.Contain("\"reason\":\"RiderDied\""));
            Assert.That(reasons[1], Does.Contain("\"reason\":\"RiderRemoved\""));
        }

        private void StartWithTwoPlayers(int maxHealth = 1000, bool blockExits = false)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0f, 2f, 8f), new Vector3(5f, 2f, 0.25f), Quaternion.Identity),
            };
            if (blockExits)
            {
                colliders.Add(WorldCollider.Box(3, 0, WorldCollider.StaticGroup, new Vector3(-2f, 1.5f, 0.3f), new Vector3(0.5f, 1f, 0.5f), Quaternion.Identity));
                colliders.Add(WorldCollider.Box(4, 0, WorldCollider.StaticGroup, new Vector3(3f, 1.5f, 0f), new Vector3(0.5f, 1f, 0.5f), Quaternion.Identity));
            }

            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var vehicles = new NetMapVehicles(new[] { Jeep(maxHealth) });
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, vehicles: vehicles);
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
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
            _vehicles = new ServerVehicles(_rules, _movement);
            _combat = new ServerCombat(_rules, _movement, _vehicles);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_vehicles);
            _host.Register(_combat);
            _host.Start(_context);
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private static MapVehicle Jeep(int maxHealth)
        {
            var gun = new WeaponSpec(_gunId, "weapon.mounted", WeaponCategory.AssaultRifle, FireMode.Auto, 100, 0, 0.1f, 2f, 30, 100f, DamageFalloff.None);
            var spec = new VehicleSpec(
                "jeep",
                maxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, ownWeaponArcDegrees: 180f),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
                },
                new[] { new VehicleWeaponSpec(_gunId, 2, pitchMinDegrees: -45f) },
                new[] { new VehiclePartSpec("engine", 2f) });
            return new MapVehicle(
                spec,
                _jeepPosition,
                Quaternion.Identity,
                new[]
                {
                    new MapVehicleSeat(new Vector3(-0.5f, 0.5f, 0.3f), 0f, new[] { _driverEnter }, new[] { _driverExit }),
                    new MapVehicleSeat(new Vector3(0.5f, 0.5f, 0.3f), 90f, new[] { new Vector3(1.5f, 0f, 0.3f) }),
                    new MapVehicleSeat(new Vector3(0f, 1.2f, -1.2f), 0f, new[] { new Vector3(0f, 0f, -2.5f) }),
                },
                new[]
                {
                    new MapVehicleHullBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.3f, 2f), Quaternion.Identity),
                    new MapVehicleHullBox(new Vector3(0f, 1f, 1.5f), new Vector3(0.5f, 0.2f, 0.5f), Quaternion.Identity, "engine"),
                },
                new[] { new Vector3(3f, 0f, 0f) },
                new[] { gun });
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void BoardDriver()
        {
            StandAt(1, _driverEnter);
            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 0, 0), Is.True, _log.ToString());
        }

        // 乗り物から見た位置 local の地面に立たせる
        private void StandAt(int id, Vector3 local)
        {
            Teleport(id, MapVehicle.RiderOrigin(_jeepPosition + local, _context.Bodies.Geometry, crouched: false));
        }

        private void Teleport(int id, Vector3 origin)
        {
            _context.Bodies.Teleport(id, origin, 0f, _host.TickCount);
        }

        private void Drive(int id, int generation, Vector3 position, Quaternion rotation)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new VehicleMoveMessage
            {
                Vehicle = 0,
                Sequence = _sequence++,
                CorrectionGeneration = generation,
                Position = position,
                Rotation = rotation,
            });
        }

        private void Walk(int id, Vector2 move, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, move, 0f, InputButtons.Sprint));
                _host.OnMessage(new ConnectionId((ulong)id), input);
                Tick();
            }
        }

        private List<VehicleCorrectedMessage> Corrections(int id)
        {
            return _transport.Read<VehicleCorrectedMessage>(
                id, m => new VehicleCorrectedMessage { Vehicle = m.Vehicle, Generation = m.Generation, Position = m.Position, Rotation = m.Rotation });
        }

        private void FireFrom(int shooter, Vector3 target, int tick, string weaponId)
        {
            var eye = Body(shooter).EyePosition;
            Fire(shooter, eye, target - eye, tick, weaponId);
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

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
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
            _combat.Contribute(snapshot, receiver);
            _vehicles.Contribute(snapshot, receiver);
            return snapshot;
        }

        private static PlayerSnapshot Row(SnapshotMessage snapshot, int playerId)
        {
            return snapshot.Players[snapshot.Players.IndexOf(playerId)];
        }

        private int Tick0()
        {
            return (int)_host.TickCount;
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

        private IEnumerable<string> Lines()
        {
            return _json.ToString().Split('\n').Where(line => line.Length > 0);
        }
    }
}
