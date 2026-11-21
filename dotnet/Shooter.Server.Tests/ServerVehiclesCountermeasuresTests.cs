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
    // 乗り物へのロックオン・乗っている人への警告・フレアで外れるロックを、移動・ルール・乗り物・戦闘を同じホストで動かして確かめる
    public sealed class ServerVehiclesCountermeasuresTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _flareCount = 2;
        private const float _flareInterval = 1f;
        private const float _flareLifetime = 2f;
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 床の上 (y=0.5) に置いたヘリ。車体の中ほどは (0, 1, 0)。フレアは真後ろ 10 m に止まったまま燃える (撒く速さ・重力 0)
        private static readonly Vector3 _heliPosition = new Vector3(0f, 0.5f, 0f);
        private static readonly Vector3 _heliCenter = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _driverEnter = new Vector3(-1.5f, 0f, 0.3f);
        private static readonly Vector3 _passengerEnter = new Vector3(1.5f, 0f, 0.3f);
        private static readonly Vector3 _flareOffset = new Vector3(0f, 0.5f, -10f);

        // フレアを積んでいない車は、ヘリから離れた所に置く
        private static readonly Vector3 _cartPosition = new Vector3(0f, 0.5f, 20f);

        // 撃つ人は +x の 30 m 先に立つ。そこから見るとフレアは車体から約 18 度の所にあり、既定の 30 度の内に入る
        private static readonly Vector3 _shooterFeet = new Vector3(30f, 0.5f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private ServerVehicles _vehicles;
        private ServerCombat _combat;
        private int _shotIndex;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
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
            _json.Dispose();
        }

        [Test]
        public void TheDriverDeploysFlaresUntilThePodIsEmpty()
        {
            Start();
            BoardDriver();

            Assert.That(_vehicles.DeployCountermeasure(1), Is.True, _log.ToString());
            Assert.That(_vehicles.DeployCountermeasure(1), Is.False, "the next flare waits for the interval");
            Step(_flareInterval + 0.05f);
            Assert.That(_vehicles.DeployCountermeasure(1), Is.True, _log.ToString());
            Step(_flareInterval + 0.05f);
            Assert.That(_vehicles.DeployCountermeasure(1), Is.False);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vehicle] 'heli#0' flare deployed rider=1 id=0 remaining=1/2"));
            Assert.That(log, Does.Contain("[Vehicle] 'heli#0' flare refused rider=1 reason=cooling_down remaining=1/2 wait=1.00"));
            Assert.That(log, Does.Contain("[Vehicle] 'heli#0' flare deployed rider=1 id=1 remaining=0/2"));
            Assert.That(log, Does.Contain("[Vehicle] 'heli#0' flare refused rider=1 reason=empty remaining=0/2"));
            Assert.That(_vehicles.CountermeasuresLeft(0), Is.Zero);
        }

        [Test]
        public void ADeployedFlareStaysAtTheEjectPointUntilItBurnsOut()
        {
            Start();
            BoardDriver();

            _vehicles.DeployCountermeasure(1);
            Tick();

            Assert.That(_vehicles.FlaresOf(0), Has.Count.EqualTo(1));
            Assert.That(Vector3.Distance(_vehicles.FlaresOf(0)[0].Position, _heliPosition + _flareOffset), Is.LessThan(1e-4f));
            Step(_flareLifetime + 0.05f);
            Assert.That(_vehicles.FlaresOf(0), Is.Empty);
        }

        [Test]
        public void TheCountermeasureRequestFromTheClientDeploysAFlare()
        {
            Start();
            BoardDriver();

            _host.OnMessage(new ConnectionId(1), new VehicleRequestMessage { Action = VehicleAction.Countermeasure });

            Assert.That(_vehicles.CountermeasuresLeft(0), Is.EqualTo(_flareCount - 1));
        }

        [Test]
        public void APassengerCannotDeployFlares()
        {
            Start();
            BoardDriver();
            BoardPassenger(2);

            Assert.That(_vehicles.DeployCountermeasure(2), Is.False);
            Assert.That(_vehicles.CountermeasuresLeft(0), Is.EqualTo(_flareCount));
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'heli#0' flare refused rider=2 reason=not_driver"));
        }

        [Test]
        public void SomeoneOnFootCannotDeployFlares()
        {
            Start();

            Assert.That(_vehicles.DeployCountermeasure(2), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] '-' flare refused rider=2 reason=not_riding"));
        }

        [Test]
        public void AVehicleWithoutFlaresRefuses()
        {
            Start();
            Teleport(1, MapVehicle.RiderOrigin(_cartPosition + _driverEnter, _context.Bodies.Geometry, crouched: false));
            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 1, 0), Is.True, _log.ToString());

            Assert.That(_vehicles.DeployCountermeasure(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Vehicle] 'cart#1' flare refused rider=1 reason=no_flares"));
            Assert.That(_vehicles.DecoyOf(ServerLockTarget.Vehicle(1)), Is.Null);
        }

        [Test]
        public void AMissileLockedOnTheHeliWarnsEveryoneAboardAndHitsIt()
        {
            Start(players: 3);
            BoardDriver();
            BoardPassenger(3);
            StandShooter();

            FireLocked(2, _heliCenter, LockTargetKind.Vehicle, 0);
            Tick();

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=v0 accepted"));
            var driver = Warnings(1);
            Assert.That(driver, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(driver[0].Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(driver[0].ProjectileId, Is.Not.EqualTo(LockOnWarningMessage.NoProjectile));
            Assert.That(driver[0].Source.X, Is.GreaterThan(20f), "the warning points at the missile, near the shooter");
            Assert.That(Warnings(3).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked }));
            Assert.That(Warnings(2), Is.Empty, "the shooter is not warned");
            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on warning player=1 stage=locked projectile=" + driver[0].ProjectileId + " target=v0"));

            Step(2f);

            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out _), Is.True);
            Assert.That(health, Is.LessThan(1000), _log.ToString());
            Assert.That(Warnings(1).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked, LockOnWarningStage.None }));
            Assert.That(Warnings(3).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked, LockOnWarningStage.None }));
            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on warning player=1 cleared"));
            Assert.That(_combat.LockOnWarnings.Warned, Is.Empty);
        }

        [Test]
        public void AFlareDeployedWhileTheMissileFliesLuresItAndClearsTheWarning()
        {
            Start();
            BoardDriver();
            StandShooter();

            FireLocked(2, _heliCenter, LockTargetKind.Vehicle, 0);
            Step(0.2f);
            Assert.That(Warnings(1).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked }), _log.ToString());

            Assert.That(_vehicles.DeployCountermeasure(1), Is.True);
            Step(1f);

            Assert.That(_log.ToString(), Does.Match(@"\[Combat\] net projectile id=\d+ weapon=" + TestRules.RifleId + " lured flare=0"));
            Assert.That(Warnings(1).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked, LockOnWarningStage.None }));
            var missile = Projectiles().Single();
            Assert.That(missile.Position.Z, Is.LessThan(-3f), "the missile turns away toward the flare behind the heli");
            Assert.That(_vehicles.TryGetState(0, out _, out _, out var health, out _), Is.True);
            Assert.That(health, Is.EqualTo(1000));
        }

        [Test]
        public void ALockIsRefusedWhileAFlareBurnsNearTheHeliAndTakenAfterItBurnsOut()
        {
            Start();
            BoardDriver();
            StandShooter();
            _vehicles.DeployCountermeasure(1);
            Tick();

            FireLocked(2, _heliCenter, LockTargetKind.Vehicle, 0);
            Tick();

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=v0 rejected reason=lured"));
            Assert.That(Warnings(1), Is.Empty, "a refused lock homes on nothing and warns nobody");

            Step(_flareLifetime + 0.1f);
            FireLocked(2, _heliCenter, LockTargetKind.Vehicle, 0);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=v0 accepted"));
        }

        [Test]
        public void ALockOnTheRiderIsAlsoBrokenByTheVehiclesFlares()
        {
            Start();
            BoardDriver();
            StandShooter();
            _vehicles.DeployCountermeasure(1);
            Tick();

            FireLocked(2, Body(1).Center, LockTargetKind.Player, 1);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=1 rejected reason=lured"));
        }

        [Test]
        public void AMissileLockedOnAPersonOnFootWarnsThatPerson()
        {
            Start();
            StandShooter();

            FireLocked(2, Body(1).Center, LockTargetKind.Player, 1);
            Tick();

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=1 accepted"));
            Assert.That(Warnings(1).Select(w => w.Stage), Is.EqualTo(new[] { LockOnWarningStage.Locked }));
        }

        [Test]
        public void ARiderCannotLockOntoTheirOwnVehicle()
        {
            Start(players: 3);
            BoardDriver();
            BoardPassenger(3);

            FireLocked(3, _heliCenter, LockTargetKind.Vehicle, 0);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=3 target=v0 rejected reason=self"));
        }

        [Test]
        public void ALockOnAVehicleThatIsNotOnTheMapIsRefused()
        {
            Start();
            StandShooter();

            FireLocked(2, _heliCenter, LockTargetKind.Vehicle, 5);

            Assert.That(_log.ToString(), Does.Contain("[Combat] net lock-on shooter=2 target=v5 rejected reason=not-hittable"));
        }

        private void Start(int players = 2)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var vehicles = new NetMapVehicles(new[] { Heli(), Cart() });
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, vehicles: vehicles);
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules, rifle: Launcher()), PingSpec.Default, "memory", null);
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
            var module = new MatchRulesModule();
            _vehicles = new ServerVehicles(module, _movement);
            _combat = new ServerCombat(module, _movement, _vehicles);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(module);
            _host.Register(_vehicles);
            _host.Register(_combat);
            _host.Start(_context);
            for (var id = 1; id <= players; id++)
            {
                Join(id);
            }

            Step(0.2f);
        }

        // ロックした相手へ曲がる、まっすぐ 20 m/秒で 3 秒飛ぶ弾
        private static WeaponSpec Launcher()
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                rifle.Round.Range,
                projectile: new ProjectileSpec(20f, 0f, 3f),
                homing: new HomingSpec(1f, 8f, 45f));
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, round, rifle.Impact, rifle.Aim, rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        private static MapVehicle Heli()
        {
            var spec = new VehicleSpec(
                "heli",
                1000,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, ownWeaponArcDegrees: VehicleSeatSpec.MaxOwnWeaponArcDegrees),
                });
            var flares = new MapVehicleCountermeasures(
                new CountermeasureSpec(_flareCount, _flareInterval, _flareLifetime, CountermeasureSpec.DefaultDecoyConeDegrees, 0f, 0f),
                _flareOffset,
                Vector3.Zero);
            return new MapVehicle(
                spec,
                _heliPosition,
                Quaternion.Identity,
                new[]
                {
                    new MapVehicleSeat(new Vector3(-0.5f, 0.5f, 0.3f), 0f, new[] { _driverEnter }),
                    new MapVehicleSeat(new Vector3(0.5f, 0.5f, 0.3f), 0f, new[] { _passengerEnter }),
                },
                new[] { new MapVehicleHullBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.3f, 2f), Quaternion.Identity) },
                new[] { new Vector3(3f, 0f, 0f) },
                countermeasures: flares);
        }

        private static MapVehicle Cart()
        {
            var spec = new VehicleSpec("cart", 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) });
            return new MapVehicle(
                spec,
                _cartPosition,
                Quaternion.Identity,
                new[] { new MapVehicleSeat(new Vector3(-0.5f, 0.5f, 0f), 0f, new[] { _driverEnter }) },
                new[] { new MapVehicleHullBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.3f, 1.5f), Quaternion.Identity) },
                new[] { new Vector3(3f, 0f, 0f) });
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

        private void BoardPassenger(int id)
        {
            StandAt(id, _passengerEnter);
            Assert.That(_vehicles.Handle(id, VehicleAction.Board, 0, 1), Is.True, _log.ToString());
        }

        private void StandShooter()
        {
            Teleport(2, MapVehicle.RiderOrigin(_shooterFeet, _context.Bodies.Geometry, crouched: false));
            Tick();
        }

        // ヘリから見た位置 local の地面に立たせる
        private void StandAt(int id, Vector3 local)
        {
            Teleport(id, MapVehicle.RiderOrigin(_heliPosition + local, _context.Bodies.Geometry, crouched: false));
        }

        private void Teleport(int id, Vector3 origin)
        {
            _context.Bodies.Teleport(id, origin, 0f, _host.TickCount);
        }

        private void FireLocked(int shooter, Vector3 aimAt, LockTargetKind kind, int target)
        {
            var eye = Body(shooter).EyePosition;
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = Vector3.Normalize(aimAt - eye),
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
                HasLockTarget = true,
                LockTargetId = target,
                LockTargetKind = kind,
            });
        }

        private List<LockOnWarningMessage> Warnings(int id)
        {
            return _transport.Read<LockOnWarningMessage>(
                id, m => new LockOnWarningMessage { Stage = m.Stage, ProjectileId = m.ProjectileId, Source = m.Source });
        }

        private List<ProjectileSnapshot> Projectiles()
        {
            var snapshot = new SnapshotMessage { HasOwner = true };
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _combat.Contribute(snapshot, receiver);
            var rows = new List<ProjectileSnapshot>();
            for (var i = 0; i < snapshot.Projectiles.Count; i++)
            {
                rows.Add(snapshot.Projectiles[i]);
            }

            return rows;
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
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
    }
}
