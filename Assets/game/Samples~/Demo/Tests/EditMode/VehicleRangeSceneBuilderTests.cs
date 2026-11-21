using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterVolume = Shooter.Runtime.Traversal.Water;

namespace Shooter.Tests.EditMode
{
    public sealed class VehicleRangeSceneBuilderTests
    {
        private const float Epsilon = 1e-3f;
        private const string HeatGunId = "0f6a2c1e-7b3d-4e59-9a84-d2c5b1e3f760";
        private const string GuidedId = "3c7d9e21-4b5a-4f6c-8d7e-9a0b1c2d3e4f";
        private const float GuidedRange = 150f;

        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        private Scene _scene;
        private WeaponDefinition _rifle;
        private WeaponDefinition _heatGun;
        private WeaponDefinition _guided;
        private GameRulesDefinition _rules;
        private AircraftDefinition _aircraft;
        private HelicopterDefinition _helicopter;
        private BoatDefinition _boat;
        private HorseDefinition _horse;
        private AudioClip _engine;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            _heatGun = NewWeapon(
                "HeatGun",
                new WeaponSpec(
                    HeatGunId,
                    "weapon.heatGun",
                    WeaponCategory.AssaultRifle,
                    trigger: new TriggerSpec(FireMode.Auto, 0.12f),
                    ammo: new AmmoSpec(1, 0, 0f, kind: AmmoKind.Heat, heat: new HeatSpec(0.1f, 0.4f, 0.3f, 1.5f)),
                    impact: new ImpactSpec(22)));
            _guided = NewWeapon(
                "Guided",
                new WeaponSpec(
                    GuidedId,
                    "weapon.guided",
                    WeaponCategory.Launcher,
                    round: new RoundSpec(
                        RoundDelivery.Projectile,
                        GuidedRange,
                        projectile: new ProjectileSpec(25f, 0f, 6f),
                        homing: new HomingSpec(1f, 8f, 90f)),
                    impact: new ImpactSpec(120)));
            _rules = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _rules.name = "Rules";
            _assets.Add(_rules);
            GameRulesAssetBuilder.Configure(_rules, GameRulesAssetBuilder.TpsRangeRules());
            _aircraft = ScriptableObject.CreateInstance<AircraftDefinition>();
            _aircraft.name = "Aircraft";
            _assets.Add(_aircraft);
            AircraftFixtureBuilder.Configure(_aircraft);
            _helicopter = ScriptableObject.CreateInstance<HelicopterDefinition>();
            _helicopter.name = "Helicopter";
            _assets.Add(_helicopter);
            VehicleFixtureBuilder.ConfigureHelicopter(_helicopter);
            _boat = ScriptableObject.CreateInstance<BoatDefinition>();
            _boat.name = "Boat";
            _assets.Add(_boat);
            VehicleFixtureBuilder.ConfigureBoat(_boat);
            _horse = ScriptableObject.CreateInstance<HorseDefinition>();
            _horse.name = "Horse";
            _assets.Add(_horse);
            VehicleFixtureBuilder.ConfigureHorse(_horse);
            _horse.Mass = 420f;
            _engine = AudioClip.Create("Engine", 4410, 1, 44100, false);
            _assets.Add(_engine);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [Test]
        public void SavesToTheVehicleRangeSceneOfTheBuildList()
        {
            Assert.That(VehicleRangeSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.VehicleRangeScene));
            Assert.That(BuildSceneList.VehicleRangeScene, Is.EqualTo(DemoSample.Root + "/Scenes/VehicleRange.unity"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.VehicleRangeScene));
            Assert.That(VehicleRangeSceneBuilder.NavMeshDataPath, Is.EqualTo(ModeSceneBuilder.NavMeshDataPathFor(BuildSceneList.VehicleRangeScene)));
            Assert.That(VehicleRangeSceneBuilder.MenuPath, Is.EqualTo("Shooter/Build All Scenes"));
        }

        [Test]
        public void TheLevelIsAFloorWithAFlatLaneASlopeAndObstacles()
        {
            var surface = VehicleRangeSceneBuilder.CreateLevel(_scene);
            Physics.SyncTransforms();

            var floor = Find(VehicleRangeSceneBuilder.FloorName);
            Assert.That(surface.gameObject, Is.SameAs(floor));
            Assert.That(surface.collectObjects, Is.EqualTo(CollectObjects.All));
            Assert.That(floor.transform.localScale, Is.EqualTo(VehicleRangeSceneBuilder.FloorScale));
            Assert.That(VehicleRangeSceneBuilder.CourseEndX, Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));

            var rise = VehicleRangeSceneBuilder.SlopeRise;
            var run = VehicleRangeSceneBuilder.SlopeRun;
            Assert.That(rise, Is.EqualTo(12f * Mathf.Sin(10f * Mathf.Deg2Rad)).Within(Epsilon));
            Assert.That(GroundHeight(15f), Is.EqualTo(0f).Within(Epsilon), "the lane is flat where the car is driven and stopped");
            Assert.That(GroundHeight(VehicleRangeSceneBuilder.SlopeStartX + (run / 2f)), Is.EqualTo(rise / 2f).Within(0.02f));
            Assert.That(GroundHeight(VehicleRangeSceneBuilder.PlateauStartX + (VehicleRangeSceneBuilder.PlateauLength / 2f)), Is.EqualTo(rise).Within(Epsilon));
            Assert.That(GroundHeight(VehicleRangeSceneBuilder.SlopeDownStartX + (run / 2f)), Is.EqualTo(rise / 2f).Within(0.02f));
            Assert.That(GroundHeight(VehicleRangeSceneBuilder.SlopeStartX + 0.05f), Is.LessThan(0.05f), "the slope starts flush with the floor");

            var course = Find(VehicleRangeSceneBuilder.CourseName).transform;
            foreach (var name in new[] { VehicleRangeSceneBuilder.SlopeUpName, VehicleRangeSceneBuilder.PlateauName, VehicleRangeSceneBuilder.SlopeDownName })
            {
                Assert.That(course.Find(name).gameObject.isStatic, Is.True, name);
            }

            var obstacles = Find(VehicleRangeSceneBuilder.ObstaclesName).transform;
            Assert.That(obstacles.childCount, Is.EqualTo(5));
            Assert.That(VehicleRangeSceneBuilder.PillarCenter(0), Is.EqualTo(new Vector3(10f, 0.75f, 20f)));
            Assert.That(VehicleRangeSceneBuilder.PillarCenter(1), Is.EqualTo(new Vector3(16f, 0.75f, 16f)));
            Assert.That(obstacles.Find(VehicleRangeSceneBuilder.PillarNamePrefix + "4").GetComponent<Collider>().bounds.min.y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void TheLevelRefusesANonEmptyScene()
        {
            SceneManager.MoveGameObjectToScene(new GameObject("Stray"), _scene);

            Assert.Throws<InvalidOperationException>(() => VehicleRangeSceneBuilder.CreateLevel(_scene));
        }

        [Test]
        public void TheGunCarWaitsBesideThePlayerWithItsDriverSeatClosest()
        {
            var bootstrap = CreateActors();

            Assert.That(bootstrap.name, Is.EqualTo(VehicleRangeSceneBuilder.BootstrapObjectName));
            Assert.That(bootstrap.CreatesFloor, Is.False);
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(new[] { _rifle }));
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(_rules));

            var gunCar = Find(VehicleRangeSceneBuilder.GunCarName).GetComponent<Vehicle>();
            Assert.That(gunCar.VehicleId, Is.EqualTo(CarFixtureBuilder.GunnerVehicleId));
            Assert.That(Vector3.Distance(gunCar.transform.position, VehicleRangeSceneBuilder.GunCarOrigin), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(gunCar.transform.forward, Vector3.right), Is.LessThan(0.1f));
            Assert.That(gunCar.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger, VehicleSeatRole.Gunner }));
            Assert.That(gunCar.FiringOf(gunCar.Seats[2]), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
            Assert.That(gunCar.WeaponMounts.Single().WeaponSpec.Id, Is.EqualTo(_rifle.Id));
            Assert.That(gunCar.GetComponent<SimpleCarMotor>(), Is.Not.Null);
            Assert.That(gunCar.GetComponent<VehicleMotorLink>(), Is.Not.Null);

            Assert.That(gunCar.FindBoardableSeat(GameBootstrap.PlayerSpawn, out var distance), Is.EqualTo(0), "Interact at the spawn takes the driver seat");
            Assert.That(distance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));

            var body = CarFixtureBuilder.BodyBounds(VehicleRangeSceneBuilder.GunCarOrigin, VehicleRangeSceneBuilder.GunCarRotation);
            Assert.That(body.Intersects(new Bounds(GameBootstrap.PlayerSpawn, new Vector3(1f, 2f, 1f))), Is.False, "the car does not touch the player");
            foreach (var dummy in GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity))
            {
                Assert.That(Mathf.Abs(dummy.z - VehicleRangeSceneBuilder.GunCarOrigin.z), Is.GreaterThan(VehicleRangeSceneBuilder.CourseWidth), "the bootstrap targets stand off the lane");
            }
        }

        [Test]
        public void TheRoofGunCanReachATargetWhereverTheCarStops()
        {
            CreateActors();

            var targets = Find(VehicleRangeSceneBuilder.TargetsName).GetComponentsInChildren<TargetDummy>();
            Assert.That(targets.Select(t => t.Damageable.Id), Is.EqualTo(new[] { "VehicleTarget0", "VehicleTarget1", "VehicleTarget2" }));

            // smoke は 10m 走ってからブレーキを掛けるので、止まるのは 10〜20m 先。
            for (var travelled = 10f; travelled <= 20f; travelled += 1f)
            {
                var origin = VehicleRangeSceneBuilder.GunCarOrigin + new Vector3(travelled, 0f, 0f);
                var gun = origin + (VehicleRangeSceneBuilder.GunCarRotation * CarFixtureBuilder.GunLocal);
                var nearest = targets.OrderBy(t => Vector3.Distance(origin, t.transform.position)).First();
                var aim = nearest.transform.TransformPoint(VehicleSmokeRun.TargetAimLocal) - gun;
                var pitch = Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                Assert.That(pitch, Is.GreaterThan(CarFixtureBuilder.GunPitchMinDegrees), $"stopping {travelled}m on");
            }
        }

        [Test]
        public void TheMountedGunFacesTheCarItDestroysFromOutsideTheBlast()
        {
            CreateActors();

            var mounted = Find(VehicleRangeSceneBuilder.MountedGunName).GetComponent<MountedGun>();
            Assert.That(mounted.IsReady, Is.True);
            Assert.That(mounted.Vehicle.VehicleId, Is.EqualTo(VehicleRangeSceneBuilder.MountedGunName));
            Assert.That(mounted.Mount.WeaponSpec.Id, Is.EqualTo(HeatGunId));

            var car = Find(VehicleRangeSceneBuilder.CarName).GetComponent<Vehicle>();
            Assert.That(car.VehicleId, Is.EqualTo(CarFixtureBuilder.VehicleId));
            Assert.That(car.Seats.Count, Is.EqualTo(2));
            Assert.That(car.GetComponent<SimpleCarMotor>(), Is.Not.Null);

            var center = car.transform.TransformPoint(CarFixtureBuilder.BodyCenter);
            Assert.That(mounted.CanAimAt(center), Is.True);
            Assert.That(
                Vector3.Distance(mounted.transform.position, car.transform.position),
                Is.GreaterThan(VehicleDurabilitySpec.DefaultExplosionRadius + 2f),
                "the car's explosion does not reach the gunner");
            Assert.That(mounted.Vehicle.Seats[0].EnterPoints.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheAircraftWaitsAtTheStartOfAClearRunway()
        {
            CreateActors();
            Physics.SyncTransforms();

            var aircraft = Find(VehicleRangeSceneBuilder.AircraftName).GetComponent<Vehicle>();
            Assert.That(aircraft.VehicleId, Is.EqualTo(AircraftFixtureBuilder.VehicleId));
            Assert.That(Vector3.Distance(aircraft.transform.position, VehicleRangeSceneBuilder.AircraftOrigin), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(aircraft.transform.forward, Vector3.forward), Is.LessThan(0.1f));
            Assert.That(aircraft.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver }));
            Assert.That(aircraft.Spec.ChaseDistance, Is.EqualTo(AircraftFixtureBuilder.ChaseDistance));

            var motor = aircraft.GetComponent<SimpleAircraftMotor>();
            var scheme = aircraft.GetComponent<AircraftControlScheme>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Body.mass, Is.EqualTo(AircraftSpec.DefaultMass), "the range asset starts from the kit defaults");
            Assert.That(
                AircraftFixtureBuilder.LeftWingCenter.x - (AircraftFixtureBuilder.WingSize.x / 2f),
                Is.EqualTo(-AircraftFixtureBuilder.WingSpan / 2f).Within(Epsilon),
                "the two wings reach the full span");
            Assert.That(
                AircraftFixtureBuilder.RightWingCenter.x - (AircraftFixtureBuilder.WingSize.x / 2f),
                Is.EqualTo(AircraftFixtureBuilder.FuselageSize.x / 2f).Within(Epsilon),
                "the wings start at the side of the fuselage");
            Assert.That(aircraft.GetComponent<VehicleMotorLink>(), Is.Not.Null);
            Assert.That(aircraft.GetComponent<VehicleImpactDamage>(), Is.Not.Null);
            Assert.That(VehicleControlSchemes.For(aircraft.gameObject), Is.SameAs(scheme));
            Assert.That(aircraft.GetComponentsInChildren<WheelCollider>().Length, Is.EqualTo(3));

            var fuselageBottom = AircraftFixtureBuilder.FuselageCenter.y - (AircraftFixtureBuilder.FuselageSize.y / 2f);
            Assert.That(fuselageBottom, Is.GreaterThan(AircraftFixtureBuilder.NoseWheel.y - AircraftFixtureBuilder.WheelRadius), "the fuselage rides above the wheels");

            Assert.That(VehicleRangeSceneBuilder.RunwayLength, Is.EqualTo(125f).Within(Epsilon));
            var halfSpan = (AircraftFixtureBuilder.WingSpan / 2f) + 1f;
            var runwayCenter = new Vector3(
                VehicleRangeSceneBuilder.AircraftOrigin.x,
                3f,
                (VehicleRangeSceneBuilder.AircraftOrigin.z + VehicleRangeSceneBuilder.FloorHalfExtent) / 2f);
            var runwayHalf = new Vector3(halfSpan, 2.9f, VehicleRangeSceneBuilder.RunwayLength / 2f);
            foreach (var hit in Physics.OverlapBox(runwayCenter, runwayHalf))
            {
                Assert.That(hit.transform.IsChildOf(aircraft.transform), Is.True, $"'{hit.name}' stands on the runway");
            }

            Assert.That(
                aircraft.FindBoardableSeat(aircraft.Seats[0].EnterPoints[0].position, out var distance),
                Is.EqualTo(0),
                "Interact at the enter point takes the pilot seat");
            Assert.That(distance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));
        }

        [Test]
        public void TheEnemyStandsBeyondEarshotOfWhatTheSmokeFires()
        {
            CreateActors();

            var enemy = Find(VehicleRangeSceneBuilder.EnemiesName).GetComponentInChildren<EnemyAgent>();
            Assert.That(enemy.Id, Is.EqualTo(VehicleRangeSceneBuilder.EnemyId));
            Assert.That(enemy.SightRange, Is.EqualTo(VehicleRangeSceneBuilder.EnemySightRange));
            Assert.That(enemy.WeaponDefinition, Is.SameAs(_rifle));

            var noise = Mathf.Max(_rifle.ToSpec().ShotNoiseRadius, _heatGun.ToSpec().ShotNoiseRadius);
            var lane = VehicleRangeSceneBuilder.GunCarOrigin;
            var shootingSpots = new[] { lane, lane + new Vector3(25f, 0f, 0f), VehicleRangeSceneBuilder.MountedGunOrigin, VehicleRangeSceneBuilder.CarOrigin };
            foreach (var spot in shootingSpots)
            {
                Assert.That(Vector3.Distance(spot, VehicleRangeSceneBuilder.EnemyGround), Is.GreaterThan(noise), spot.ToString());
            }

            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.EnemyGround.x), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));
            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.EnemyGround.z), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));
        }

        [Test]
        public void ActorsNeedWeaponsRulesAndEveryVehicleAsset()
        {
            var rifle = new[] { _rifle };
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, null, _heatGun, _rules, _aircraft, _helicopter, _boat, _horse, _guided));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, null, _rules, _aircraft, _helicopter, _boat, _horse, _guided));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, null, _aircraft, _helicopter, _boat, _horse, _guided));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, _rules, null, _helicopter, _boat, _horse, _guided));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, _rules, _aircraft, null, _boat, _horse, _guided));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, _rules, _aircraft, _helicopter, null, _horse, _guided));
            var horseError = Assert.Throws<ArgumentNullException>(
                () => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, _rules, _aircraft, _helicopter, _boat, null, _guided));
            Assert.That(horseError.ParamName, Is.EqualTo("horse"));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, rifle, _heatGun, _rules, _aircraft, _helicopter, _boat, _horse, null));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed when an argument is missing");
        }

        [Test]
        public void AnInvalidAircraftAssetPlacesNothingAndNamesTheAsset()
        {
            _aircraft.MaxThrust = -1f;

            var error = Assert.Throws<ArgumentException>(() => CreateActors());

            Assert.That(error.Message, Does.Contain("'Aircraft'"));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void InvalidHelicopterOrBoatAssetsPlaceNothingAndNameTheAsset()
        {
            _helicopter.MaxSpeed = -1f;
            var helicopterError = Assert.Throws<ArgumentException>(() => CreateActors());
            Assert.That(helicopterError.Message, Does.Contain("'Helicopter'"));
            Assert.That(_scene.rootCount, Is.Zero);

            VehicleFixtureBuilder.ConfigureHelicopter(_helicopter);
            _boat.Draft = _boat.HullHeight + 1f;
            var boatError = Assert.Throws<ArgumentException>(() => CreateActors());
            Assert.That(boatError.Message, Does.Contain("'Boat'"));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void AnInvalidHorseAssetPlacesNothingAndNamesTheAsset()
        {
            _horse.WalkSpeed = _horse.TrotSpeed + 1f;

            var error = Assert.Throws<ArgumentException>(() => CreateActors());

            Assert.That(error.Message, Does.Contain("'Horse'"));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void AWeaponWithoutHomingCannotArmTheTurret()
        {
            var error = Assert.Throws<ArgumentException>(
                () => VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules, _aircraft, _helicopter, _boat, _horse, _rifle));

            Assert.That(error.ParamName, Is.EqualTo("lockOnWeapon"));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void TheTurretWeaponIsPickedByItsHomingRoundNotItsName()
        {
            Assert.That(VehicleRangeSceneBuilder.PickLockOnWeapon(new[] { _rifle, null, _heatGun, _guided }), Is.SameAs(_guided));
            Assert.That(VehicleRangeSceneBuilder.PickLockOnWeapon(new[] { _rifle, _heatGun }), Is.Null);
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.PickLockOnWeapon(null));
        }

        [Test]
        public void TheGroundReachesFarPastTheFloorWithoutCoveringIt()
        {
            VehicleRangeSceneBuilder.CreateLevel(_scene);
            VehicleRangeSceneBuilder.CreateSurroundings(_scene);
            Physics.SyncTransforms();

            var outer = Find(VehicleRangeSceneBuilder.OuterGroundName);
            Assert.That(outer.transform.position.y, Is.EqualTo(-VehicleRangeSceneBuilder.OuterGroundDrop).Within(Epsilon));
            Assert.That(VehicleRangeSceneBuilder.OuterGroundHalfExtent, Is.EqualTo(2000f).Within(Epsilon));
            Assert.That(outer.isStatic, Is.True);

            Assert.That(Physics.Raycast(new Vector3(0f, 10f, 10f), Vector3.down, out var onFloor, 20f), Is.True);
            Assert.That(onFloor.collider.name, Is.EqualTo(VehicleRangeSceneBuilder.FloorName), "the floor stays on top where the two overlap");

            // 飛行機は滑走路の先の北へ 1km 近く飛んでから脱出するので、その下にも地面がある。
            Assert.That(Physics.Raycast(new Vector3(VehicleRangeSceneBuilder.AircraftOrigin.x, 50f, 1500f), Vector3.down, out var outside, 100f), Is.True);
            Assert.That(outside.collider.name, Is.EqualTo(VehicleRangeSceneBuilder.OuterGroundName));
        }

        [Test]
        public void TheOuterGroundIsWalledInAlongItsEdgesAndTheFloorIsNot()
        {
            VehicleRangeSceneBuilder.CreateLevel(_scene);
            VehicleRangeSceneBuilder.CreateSurroundings(_scene);
            Physics.SyncTransforms();

            Assert.That(Find(SceneBuildHelpers.PerimeterName), Is.Not.Null);
            SceneBuildHelpersTests.AssertPerimeterEncloses(_scene, -2000f, 2000f, -2000f, 2000f, -0.02f);

            Assert.That(
                Physics.Raycast(new Vector3(0f, 1f, 60f), Vector3.forward, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                Is.False,
                "nothing stands across the north edge of the floor");
        }

        [Test]
        public void ThePoolIsWaterWithASurfaceMarkThatDoesNotStopAnyone()
        {
            VehicleRangeSceneBuilder.CreateSurroundings(_scene);

            var pool = Find(VehicleRangeSceneBuilder.PoolName).transform;
            Assert.That(pool.position, Is.EqualTo(VehicleRangeSceneBuilder.PoolGround));

            var water = pool.Find(VehicleRangeSceneBuilder.WaterName).GetComponent<WaterVolume>();
            Assert.That(water, Is.Not.Null);
            Assert.That(water.Size, Is.EqualTo(VehicleRangeSceneBuilder.PoolSize));
            Assert.That(water.SurfaceHeight, Is.EqualTo(VehicleRangeSceneBuilder.PoolSurfaceHeight));
            Assert.That(water.Box.SurfaceY, Is.EqualTo(VehicleRangeSceneBuilder.PoolSurfaceHeight).Within(Epsilon));

            var surface = pool.Find(VehicleRangeSceneBuilder.WaterSurfaceName);
            Assert.That(surface.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(surface.position.y, Is.LessThan(VehicleRangeSceneBuilder.PoolSurfaceHeight));

            var half = VehicleRangeSceneBuilder.PoolSize / 2f;
            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.PoolGround.x) + half.x, Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));
            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.PoolGround.z) + half.z, Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));
            foreach (var target in VehicleRangeSceneBuilder.TargetGrounds)
            {
                Assert.That(Mathf.Abs(target.z - VehicleRangeSceneBuilder.PoolGround.z), Is.GreaterThan(half.z), "the pool stays south of the targets");
            }

            Assert.That(VehicleRangeSceneBuilder.PoolGround.z + half.z, Is.LessThan(VehicleRangeSceneBuilder.GunCarOrigin.z - VehicleRangeSceneBuilder.CourseWidth));
        }

        [Test]
        public void TheHelicopterFliesOnItsAssetAndBoardsFromTheSide()
        {
            CreateActors();
            Physics.SyncTransforms();

            var helicopter = Find(VehicleRangeSceneBuilder.HelicopterName).GetComponent<Vehicle>();
            Assert.That(helicopter.VehicleId, Is.EqualTo(VehicleFixtureBuilder.HelicopterVehicleId));
            Assert.That(helicopter.transform.position, Is.EqualTo(VehicleRangeSceneBuilder.HelicopterOrigin));
            Assert.That(helicopter.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger }));
            Assert.That(helicopter.Spec.ChaseDistance, Is.EqualTo(VehicleFixtureBuilder.HelicopterChaseDistance));

            var motor = helicopter.GetComponent<SimpleHelicopterMotor>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Definition, Is.SameAs(_helicopter));
            Assert.That(motor.Body.mass, Is.EqualTo(_helicopter.Mass));
            Assert.That(VehicleControlSchemes.For(helicopter.gameObject), Is.InstanceOf<HelicopterControlScheme>());
            Assert.That(helicopter.GetComponent<VehicleMotorLink>(), Is.Not.Null);

            var rotor = helicopter.transform.Find(VehicleFixtureBuilder.HelicopterRotorName);
            Assert.That(rotor.GetComponentsInChildren<Collider>(), Is.Empty, "the rotor does not knock the helicopter about");

            var lowest = helicopter.GetComponentsInChildren<Collider>().Min(c => c.bounds.min.y);
            Assert.That(lowest, Is.EqualTo(VehicleRangeSceneBuilder.HelicopterOrigin.y).Within(Epsilon), "the skids stand on the floor");

            Assert.That(helicopter.FindBoardableSeat(helicopter.Seats[0].EnterPoints[0].position, out var distance), Is.EqualTo(0));
            Assert.That(distance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));

            // smoke は機首の向きへ 10m 進めるので、その先も床の上で、Bot の車の道からも離れている。
            var ahead = VehicleRangeSceneBuilder.HelicopterOrigin
                + (VehicleRangeSceneBuilder.HelicopterRotation * Vector3.forward * (VehicleSmokeRun.HelicopterAdvanceDistance + VehicleFixtureBuilder.RotorSpan));
            Assert.That(Mathf.Abs(ahead.x), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent));
            foreach (var point in VehicleRangeSceneBuilder.BotCarRoutePoints)
            {
                Assert.That(Mathf.Abs(point.z - VehicleRangeSceneBuilder.HelicopterOrigin.z), Is.GreaterThan(VehicleFixtureBuilder.RotorSpan * 2f));
            }
        }

        [Test]
        public void TheBoatRestsAfloatInThePoolWithRoomToSailAhead()
        {
            CreateActors();
            Physics.SyncTransforms();

            var boat = Find(VehicleRangeSceneBuilder.BoatName).GetComponent<Vehicle>();
            var hull = _boat.ToSpec();
            Assert.That(boat.VehicleId, Is.EqualTo(VehicleFixtureBuilder.BoatVehicleId));
            Assert.That(boat.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger }));
            Assert.That(boat.transform.position.y, Is.EqualTo(VehicleRangeSceneBuilder.PoolSurfaceHeight + 0.2f).Within(Epsilon), "the default hull sits 0.3m deep");
            Assert.That(VehicleRangeSceneBuilder.BoatOrigin(hull), Is.EqualTo(boat.transform.position));

            var motor = boat.GetComponent<SimpleBoatMotor>();
            Assert.That(motor.Definition, Is.SameAs(_boat));
            Assert.That(motor.Body.mass, Is.EqualTo(_boat.Mass));
            Assert.That(boat.GetComponent<VehicleMotorLink>(), Is.Not.Null);

            var box = boat.transform.Find(VehicleFixtureBuilder.HullName).GetComponent<BoxCollider>().bounds;
            Assert.That(box.size.x, Is.EqualTo(hull.Width).Within(Epsilon));
            Assert.That(box.size.z, Is.EqualTo(hull.Length).Within(Epsilon));
            Assert.That(box.min.y, Is.EqualTo(boat.transform.position.y + hull.HullBottom).Within(Epsilon), "the hull box bottoms out where the buoyancy is measured");
            Assert.That(box.min.y, Is.GreaterThan(VehicleRangeSceneBuilder.PoolGround.y), "the hull floats clear of the floor");

            var half = VehicleRangeSceneBuilder.PoolSize / 2f;
            var start = boat.transform.position;
            var end = start + (VehicleRangeSceneBuilder.BoatRotation * Vector3.forward * (VehicleSmokeRun.SailDistance + hull.Length));
            foreach (var point in new[] { start, end })
            {
                Assert.That(Mathf.Abs(point.x - VehicleRangeSceneBuilder.PoolGround.x), Is.LessThan(half.x - hull.Width), point.ToString());
                Assert.That(Mathf.Abs(point.z - VehicleRangeSceneBuilder.PoolGround.z), Is.LessThan(half.z - (hull.Length / 2f)), point.ToString());
            }

            Assert.That(boat.FindBoardableSeat(boat.Seats[0].EnterPoints[0].position, out var distance), Is.EqualTo(0));
            Assert.That(distance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));
        }

        [Test]
        public void TheHorseRunsOnItsAssetWithARiderAndAPassengerWhoShootsBesideAndBehind()
        {
            CreateActors();

            var horse = Find(VehicleRangeSceneBuilder.HorseName).GetComponent<Vehicle>();
            Assert.That(horse, Is.Not.Null);
            Assert.That(horse.VehicleId, Is.EqualTo("horse"));
            Assert.That(VehicleFixtureBuilder.HorseVehicleId, Is.EqualTo("horse"));
            Assert.That(VehicleFixtureBuilder.HorseDefinitionPath, Does.EndWith("Vehicles/Horse.asset"));
            Assert.That(horse.transform.position, Is.EqualTo(VehicleRangeSceneBuilder.HorseOrigin));
            Assert.That(Vector3.Angle(horse.transform.forward, Vector3.right), Is.LessThan(0.1f));

            Assert.That(horse.Seats.Count, Is.EqualTo(2));
            Assert.That(horse.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger }));
            Assert.That(horse.Seats.Select(s => s.name), Is.EqualTo(new[] { "Seat_Rider", "Seat_Passenger" }));
            Assert.That(horse.FiringOf(horse.Seats[0]), Is.EqualTo(VehicleSeatFiring.None), "the rider holds the reins");
            Assert.That(horse.FiringOf(horse.Seats[1]), Is.EqualTo(VehicleSeatFiring.OwnWeapon));
            var passenger = horse.Spec.Seats[1];
            Assert.That(passenger.OwnWeaponArcDegrees, Is.EqualTo(300f));
            Assert.That(passenger.AllowsOwnWeaponAt(90f), Is.True, "beside the horse");
            Assert.That(passenger.AllowsOwnWeaponAt(180f), Is.True, "behind the horse");
            Assert.That(passenger.AllowsOwnWeaponAt(0f), Is.False, "not through the rider's back");
            Assert.That(horse.Seats[1].transform.localPosition.z, Is.LessThan(horse.Seats[0].transform.localPosition.z), "the passenger sits behind the rider");

            var motor = horse.GetComponent<SimpleHorseMotor>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Definition, Is.SameAs(_horse));
            Assert.That(motor.Body.mass, Is.EqualTo(420f), "the mass comes from the asset, not the default");
            Assert.That(motor.Body.constraints & RigidbodyConstraints.FreezeRotationX, Is.EqualTo(RigidbodyConstraints.FreezeRotationX));
            Assert.That(motor.Body.constraints & RigidbodyConstraints.FreezeRotationZ, Is.EqualTo(RigidbodyConstraints.FreezeRotationZ));
            var scheme = horse.GetComponent<HorseControlScheme>();
            Assert.That(scheme, Is.Not.Null);
            Assert.That(VehicleControlSchemes.For(horse.gameObject), Is.SameAs(scheme));
            var gait = horse.GetComponent<HorseGaitAnimator>();
            Assert.That(gait, Is.Not.Null);
            Assert.That(gait.Animator, Is.Null, "the box-and-cylinder shape has nothing to animate");
            Assert.That(horse.GetComponent<VehicleMotorLink>(), Is.Not.Null);

            Assert.That(horse.FindBoardableSeat(horse.Seats[0].EnterPoints[0].position, out var riderDistance), Is.EqualTo(0));
            Assert.That(riderDistance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));
            Assert.That(horse.FindBoardableSeat(horse.Seats[1].EnterPoints[0].position, out var passengerDistance), Is.EqualTo(1));
            Assert.That(passengerDistance, Is.LessThanOrEqualTo(VehicleSpec.DefaultEnterRange));
        }

        [Test]
        public void WithoutAModelTheHorseIsBuiltFromBoxesAndCylindersStandingOnItsHooves()
        {
            CreateActors();
            Physics.SyncTransforms();

            var horse = Find(VehicleRangeSceneBuilder.HorseName).transform;
            Assert.That(horse.Find(VehicleFixtureBuilder.HorseModelName), Is.Null);
            var shape = horse.Find(VehicleFixtureBuilder.HorseShapeName);
            Assert.That(shape, Is.Not.Null);
            foreach (var part in new[] { "Torso", "Neck", "Head", "Leg_0", "Leg_1", "Leg_2", "Leg_3" })
            {
                Assert.That(shape.Find(part), Is.Not.Null, part);
                Assert.That(shape.Find(part).GetComponent<MeshRenderer>(), Is.Not.Null, part);
            }

            Assert.That(shape.GetComponentsInChildren<Collider>(), Is.Empty, "the shape is only a mark");
            Assert.That(shape.Find("Leg_0").GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("Cylinder"));

            var torso = shape.Find("Torso").GetComponent<Renderer>().bounds;
            var leg = shape.Find("Leg_0").GetComponent<Renderer>().bounds;
            var head = shape.Find("Head").GetComponent<Renderer>().bounds;
            Assert.That(leg.min.y, Is.EqualTo(VehicleRangeSceneBuilder.HorseOrigin.y).Within(Epsilon), "the hooves touch the ground");
            Assert.That(leg.max.y, Is.EqualTo(torso.min.y).Within(Epsilon), "the legs carry the torso");
            Assert.That(head.min.y, Is.GreaterThan(torso.max.y), "the head is held above the back");
            Assert.That(head.center.x, Is.GreaterThan(torso.max.x - 0.5f), "the head is ahead of the torso, facing east");

            var colliders = horse.GetComponentsInChildren<Collider>();
            Assert.That(colliders.Length, Is.EqualTo(1));
            Assert.That(colliders[0].transform, Is.SameAs(horse));
            Assert.That(colliders[0].bounds.min.y, Is.EqualTo(VehicleRangeSceneBuilder.HorseOrigin.y).Within(Epsilon));
            Assert.That(colliders[0].bounds.max.y, Is.EqualTo(1.6f).Within(Epsilon), "the body box reaches the back");
        }

        [Test]
        public void TheHorseStandsOnTheFloorClearOfEveryOtherVehicleWithRoomToGallopEast()
        {
            VehicleRangeSceneBuilder.CreateLevel(_scene);
            CreateActors();
            Physics.SyncTransforms();

            var horse = Find(VehicleRangeSceneBuilder.HorseName).GetComponent<Vehicle>();
            var motor = horse.GetComponent<SimpleHorseMotor>();
            motor.Step(0f);
            Assert.That(motor.IsGrounded, Is.True, "the hooves are on the floor");

            var body = horse.GetComponent<BoxCollider>().bounds;
            foreach (var hit in Physics.OverlapBox(body.center + (Vector3.up * 0.05f), body.extents + new Vector3(1f, 0f, 1f), Quaternion.identity))
            {
                Assert.That(hit.transform.IsChildOf(horse.transform), Is.True, $"'{hit.name}' crowds the horse");
            }

            var others = _scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Vehicle>())
                .Where(v => v != horse)
                .ToList();
            Assert.That(others, Is.Not.Empty);
            foreach (var other in others)
            {
                Assert.That(
                    Vector3.Distance(other.transform.position, horse.transform.position),
                    Is.GreaterThan(VehicleSpec.DefaultEnterRange * 4f),
                    $"Interact beside the horse never boards '{other.name}'");
                foreach (var seat in horse.Seats)
                {
                    Assert.That(other.FindBoardableSeat(seat.EnterPoints[0].position, out _), Is.LessThan(0), $"'{other.name}' from {seat.name}");
                }
            }

            // 床の東の端まで、柱にもコースにも乗り物にも当たらずに駆けられる。
            var run = VehicleRangeSceneBuilder.FloorHalfExtent - body.max.x;
            var hits = Physics.BoxCastAll(
                body.center + (Vector3.up * 0.1f), body.extents * 0.9f, Vector3.right, Quaternion.identity, run, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Assert.That(hit.transform.IsChildOf(horse.transform), Is.True, $"'{hit.collider.name}' stands in the horse's path");
            }

            Assert.That(
                Vector3.Distance(VehicleRangeSceneBuilder.HorseOrigin, VehicleRangeSceneBuilder.EnemyGround),
                Is.GreaterThan(VehicleRangeSceneBuilder.EnemySightRange),
                "the enemy does not see the horse where it waits");
            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.HorseOrigin.z), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent - 5f));
        }

        [Test]
        public void AHorseModelReplacesTheShapeAndDrivesTheGaitAnimator()
        {
            var model = new GameObject("HorseArt");
            SceneManager.MoveGameObjectToScene(model, _scene);
            var rig = new GameObject("Rig");
            rig.transform.SetParent(model.transform, false);
            rig.AddComponent<Animator>();

            var horse = VehicleFixtureBuilder.CreateHorse(_scene, null, "Mount", new Vector3(1f, 0f, 2f), Quaternion.identity, _horse, model);

            var instance = horse.transform.Find(VehicleFixtureBuilder.HorseModelName);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(horse.transform.Find(VehicleFixtureBuilder.HorseShapeName), Is.Null, "a model leaves out the box-and-cylinder shape");
            Assert.That(horse.GetComponent<HorseGaitAnimator>().Animator, Is.SameAs(instance.GetComponentInChildren<Animator>()));
            Assert.That(model.transform.parent, Is.Null, "the source model is copied, not moved");
            Assert.That(horse.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger }));
            Assert.That(horse.GetComponent<BoxCollider>().size, Is.EqualTo(VehicleFixtureBuilder.HorseBodyColliderSize), "the model does not change where the horse is hit");
            Assert.That(horse.GetComponent<SimpleHorseMotor>().Definition, Is.SameAs(_horse));
        }

        [Test]
        public void AHorseWithoutAnAssetRunsOnTheDefaultsAndBadArgumentsPlaceNothing()
        {
            var horse = VehicleFixtureBuilder.CreateHorse(_scene, null, "Plain", Vector3.zero, Quaternion.identity);
            Assert.That(horse.GetComponent<SimpleHorseMotor>().Definition, Is.Null);
            Assert.That(horse.GetComponent<SimpleHorseMotor>().Body.mass, Is.EqualTo(HorseSpec.DefaultMass));

            var count = _scene.rootCount;
            Assert.Throws<ArgumentException>(() => VehicleFixtureBuilder.CreateHorse(_scene, null, " ", Vector3.zero, Quaternion.identity));
            Assert.Throws<ArgumentException>(() => VehicleFixtureBuilder.CreateHorse(default, null, "Plain", Vector3.zero, Quaternion.identity));
            _horse.WalkSpeed = _horse.TrotSpeed + 1f;
            Assert.Throws<ArgumentException>(() => VehicleFixtureBuilder.CreateHorse(_scene, null, "Plain", Vector3.zero, Quaternion.identity, _horse));
            Assert.That(_scene.rootCount, Is.EqualTo(count));
        }

        [Test]
        public void TheBotCarLoopsTheWestStripApartFromTheCarTheSmokeDestroys()
        {
            CreateActors();

            var botCar = Find(VehicleRangeSceneBuilder.BotCarName).GetComponent<Vehicle>();
            var target = Find(VehicleRangeSceneBuilder.CarName).GetComponent<Vehicle>();
            Assert.That(botCar.VehicleId, Is.EqualTo(VehicleRangeSceneBuilder.BotCarVehicleId));
            Assert.That(target.VehicleId, Is.EqualTo(CarFixtureBuilder.VehicleId), "the smoke still finds the car it destroys by its id");
            Assert.That(botCar.OccupantOf(botCar.Spec.DriverSeatIndex), Is.Null, "the stand-in driver sits down only in play");

            var driver = botCar.GetComponent<BotVehicleDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.DrivesUnmanned, Is.True);
            Assert.That(driver.Route, Is.SameAs(Find(VehicleRangeSceneBuilder.BotCarRouteName).GetComponent<VehicleRoute>()));
            Assert.That(driver.Route.Loop, Is.True);
            Assert.That(driver.Route.Points.Select(p => p.position), Is.EqualTo(VehicleRangeSceneBuilder.BotCarRoutePoints));

            var runwayEdge = VehicleRangeSceneBuilder.AircraftOrigin.x - (AircraftFixtureBuilder.WingSpan / 2f);
            foreach (var point in VehicleRangeSceneBuilder.BotCarRoutePoints)
            {
                Assert.That(point.x + CarFixtureBuilder.BodySize.x, Is.LessThan(runwayEdge - 5f), $"{point} keeps off the runway");
                Assert.That(Mathf.Abs(point.x), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent - 5f));
                Assert.That(Mathf.Abs(point.z), Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent - 5f));
                Assert.That(
                    Vector3.Distance(point, VehicleRangeSceneBuilder.CarOrigin),
                    Is.GreaterThan(VehicleDurabilitySpec.DefaultExplosionRadius + 2f),
                    "the destroyed car's blast does not reach the loop");
            }

            var toFirst = VehicleRangeSceneBuilder.BotCarRoutePoints[0] - VehicleRangeSceneBuilder.BotCarOrigin;
            Assert.That(Vector3.Angle(botCar.transform.forward, toFirst), Is.LessThan(1f), "the car starts facing its first point");
        }

        [Test]
        public void TheBotAircraftTakesOffOutsideTheFloorAndCirclesAboveTheTurret()
        {
            CreateActors();

            var plane = Find(VehicleRangeSceneBuilder.BotAircraftName).GetComponent<Vehicle>();
            Assert.That(plane.VehicleId, Is.EqualTo(VehicleRangeSceneBuilder.BotAircraftVehicleId));
            Assert.That(plane.GetComponent<VehicleCountermeasures>(), Is.Null, "only the player's aircraft carries flares");

            var pilot = plane.GetComponent<BotAircraftPilot>();
            Assert.That(pilot, Is.Not.Null);
            Assert.That(pilot.FollowWaypointHeight, Is.True);
            Assert.That(pilot.Waypoints.Select(w => w.position), Is.EqualTo(VehicleRangeSceneBuilder.BotAircraftWaypoints));
            Assert.That(pilot.Waypoints.Select(w => w.parent.name), Has.All.EqualTo(VehicleRangeSceneBuilder.BotAircraftWaypointsName));

            Assert.That(Mathf.Abs(VehicleRangeSceneBuilder.BotAircraftOrigin.x), Is.GreaterThan(VehicleRangeSceneBuilder.FloorHalfExtent + AircraftFixtureBuilder.WingSpan));
            Assert.That(VehicleRangeSceneBuilder.BotAircraftAltitude, Is.GreaterThan(GuidedRange), "the turret never reaches the bot");
            var waypoints = VehicleRangeSceneBuilder.BotAircraftWaypoints;
            for (var i = 0; i < waypoints.Length; i++)
            {
                var next = waypoints[(i + 1) % waypoints.Length];
                Assert.That(Vector3.Distance(waypoints[i], next), Is.GreaterThanOrEqualTo(600f), "points are further apart than the turning circle");
                Assert.That(Mathf.Abs(waypoints[i].x), Is.LessThan(VehicleRangeSceneBuilder.OuterGroundHalfExtent));
                Assert.That(Mathf.Abs(waypoints[i].z), Is.LessThan(VehicleRangeSceneBuilder.OuterGroundHalfExtent));
            }

            var toFirst = Vector3.ProjectOnPlane(waypoints[0] - VehicleRangeSceneBuilder.BotAircraftOrigin, Vector3.up);
            Assert.That(Vector3.Angle(plane.transform.forward, toFirst), Is.LessThan(10f), "the take-off run points at the first waypoint");
        }

        [Test]
        public void TheAircraftCarriesFlaresAndBailsOutInTheAir()
        {
            CreateActors();

            var aircraft = Find(VehicleRangeSceneBuilder.AircraftName).GetComponent<Vehicle>();
            Assert.That(aircraft.VehicleId, Is.EqualTo(AircraftFixtureBuilder.VehicleId));
            Assert.That(aircraft.Spec.BailOut.Enabled, Is.True);
            Assert.That(aircraft.Spec.BailOut.MinHeight, Is.EqualTo(BailOutSpec.DefaultMinHeight));
            Assert.That(aircraft.Spec.Parts.Select(p => p.Id), Is.EquivalentTo(AircraftDamageSpec.Default.Parts.Select(p => p.Id)), "re-specifying keeps the parts");

            var flares = aircraft.GetComponent<VehicleCountermeasures>();
            Assert.That(flares, Is.Not.Null);
            Assert.That(flares.Settings.Count, Is.EqualTo(CountermeasureSpec.DefaultCount));
            Assert.That(flares.Settings.EjectLocalOffset, Is.EqualTo(VehicleFixtureBuilder.AircraftFlareEjectLocal));
            Assert.That(flares.Settings.FlarePrefab, Is.Null, "an empty slot falls back to the glowing ball");
            Assert.That(flares.Remaining, Is.EqualTo(CountermeasureSpec.DefaultCount));
        }

        [Test]
        public void TheLockOnTurretUsesTheHomingWeaponAndCoversTheClimbOut()
        {
            CreateActors();

            var turret = Find(VehicleRangeSceneBuilder.LockOnTurretName).GetComponent<LockOnTurret>();
            Assert.That(turret, Is.Not.Null);
            Assert.That(turret.Weapon, Is.SameAs(_guided));
            Assert.That(turret.Head.name, Is.EqualTo(VehicleRangeSceneBuilder.LockOnTurretHeadName));
            Assert.That(turret.Head.GetComponentsInChildren<Collider>(), Is.Empty, "the head does not block its own line of sight");
            Assert.That(turret.Spec.MinTargetHeight, Is.EqualTo(LockOnTurretSpec.DefaultMinTargetHeight));
            Assert.That(Find(VehicleRangeSceneBuilder.LockOnWarningName).GetComponent<LockOnWarningPublisher>(), Is.Not.Null);

            // 離陸して 15m まで上がったころの飛行機が射程に入る。
            var climbing = new Vector3(VehicleRangeSceneBuilder.AircraftOrigin.x, 15f, 60f);
            Assert.That(Vector3.Distance(turret.Head.position, climbing), Is.LessThan(GuidedRange));
            Assert.That(
                Vector3.Distance(VehicleRangeSceneBuilder.LockOnTurretOrigin, VehicleRangeSceneBuilder.HelicopterOrigin),
                Is.GreaterThan(VehicleSpec.DefaultEnterRange * 4f),
                "the turret does not crowd the helicopter pad");
            Assert.That(turret.Spec.TargetTag, Is.Empty, "the vehicle range turret takes anyone in the air");
        }

        [Test]
        public void ALockOnTurretCanBePlacedUnderAParentWithItsOwnSettings()
        {
            var parent = new GameObject("Parent");
            SceneManager.MoveGameObjectToScene(parent, _scene);
            var spec = new LockOnTurretSpec(45f, 0f, "Player");
            var rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

            var turret = VehicleRangeSceneBuilder.CreateLockOnTurret(
                _scene, parent.transform, "Watch", new Vector3(3f, 0f, 4f), rotation, _guided, spec);

            Assert.That(turret.transform.parent, Is.SameAs(parent.transform));
            Assert.That(turret.name, Is.EqualTo("Watch"));
            Assert.That(Vector3.Distance(turret.transform.position, new Vector3(3f, 0f, 4f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Angle(turret.Head.forward, Vector3.right), Is.LessThan(0.01f));
            Assert.That(turret.Weapon, Is.SameAs(_guided));
            Assert.That(turret.Spec.TurnDegreesPerSecond, Is.EqualTo(45f));
            Assert.That(turret.Spec.MinTargetHeight, Is.EqualTo(0f));
            Assert.That(turret.Spec.TargetTag, Is.EqualTo("Player"));

            var count = _scene.rootCount;
            Assert.Throws<ArgumentNullException>(
                () => VehicleRangeSceneBuilder.CreateLockOnTurret(_scene, null, "Watch", Vector3.zero, rotation, null, spec));
            Assert.Throws<ArgumentNullException>(
                () => VehicleRangeSceneBuilder.CreateLockOnTurret(_scene, null, "Watch", Vector3.zero, rotation, _guided, null));
            Assert.Throws<ArgumentException>(
                () => VehicleRangeSceneBuilder.CreateLockOnTurret(_scene, null, "Watch", Vector3.zero, rotation, _heatGun, spec));
            Assert.Throws<ArgumentException>(
                () => VehicleRangeSceneBuilder.CreateLockOnTurret(_scene, null, " ", Vector3.zero, rotation, _guided, spec));
            Assert.Throws<ArgumentException>(
                () => VehicleRangeSceneBuilder.CreateLockOnTurret(default, null, "Watch", Vector3.zero, rotation, _guided, spec));
            Assert.That(_scene.rootCount, Is.EqualTo(count), "nothing is placed when the arguments are refused");
        }

        [Test]
        public void TheAircraftCarriesItsAssetWheelsPartsEffectsAndStatus()
        {
            CreateActors(_engine);

            var aircraft = Find(VehicleRangeSceneBuilder.AircraftName).GetComponent<Vehicle>();
            var motor = aircraft.GetComponent<SimpleAircraftMotor>();
            Assert.That(motor.Definition, Is.SameAs(_aircraft));
            Assert.That(motor.Body.mass, Is.EqualTo(_aircraft.Mass));
            Assert.That(aircraft.GetComponent<AircraftControlScheme>().Definition, Is.SameAs(_aircraft));

            var wheels = aircraft.GetComponent<AircraftWheels>();
            Assert.That(wheels, Is.Not.Null);
            Assert.That(wheels.Definition, Is.SameAs(_aircraft));
            Assert.That(wheels.Wheels.Count, Is.EqualTo(3));
            Assert.That(wheels.FrontWheels.Select(w => w.name), Is.EqualTo(new[] { "Wheel_Nose" }), "only the nose wheel is ahead of the centre of mass");

            var damage = aircraft.GetComponent<AircraftPartDamage>();
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.Definition, Is.SameAs(_aircraft));
            Assert.That(damage.Vehicle, Is.SameAs(aircraft));

            var expected = new[]
            {
                (AircraftFixtureBuilder.LeftWingName, AircraftDamageSpec.DefaultLeftWingId),
                (AircraftFixtureBuilder.RightWingName, AircraftDamageSpec.DefaultRightWingId),
                (AircraftFixtureBuilder.TailName, AircraftDamageSpec.DefaultTailId),
                (AircraftFixtureBuilder.EngineName, AircraftDamageSpec.DefaultEngineId),
            };
            var parts = aircraft.GetComponentsInChildren<VehiclePart>();
            Assert.That(parts.Select(p => (p.name, p.PartId)), Is.EquivalentTo(expected));
            foreach (var part in parts)
            {
                Assert.That(damage.Damage.Contains(part.PartId), Is.True, part.PartId);
                Assert.That(aircraft.Spec.FindPart(part.PartId), Is.Not.Null, $"'{part.PartId}' is in the vehicle spec, so hits on it are not unscaled");
                Assert.That(part.GetComponent<Hitbox>(), Is.Not.Null, part.PartId);
                Assert.That(part.GetComponentsInChildren<Collider>(), Is.Not.Empty, $"'{part.PartId}' can be hit");
            }

            var effects = aircraft.GetComponent<AircraftEffects>();
            Assert.That(effects, Is.Not.Null);
            Assert.That(effects.Motor, Is.SameAs(motor));
            Assert.That(effects.Spec.ElevatorAngle, Is.EqualTo(_aircraft.ToEffectsSpec().ElevatorAngle), "the effects read the motor's asset");
            Assert.That(effects.EngineClip, Is.SameAs(_engine));
            Assert.That(
                effects.Surfaces.Select(s => s.Role),
                Is.EquivalentTo(new[]
                {
                    AircraftControlSurfaceRole.Aileron, AircraftControlSurfaceRole.Aileron, AircraftControlSurfaceRole.Elevator, AircraftControlSurfaceRole.Rudder,
                }));
            Assert.That(effects.Surfaces.Select(s => s.Transform), Has.None.Null);
            Assert.That(effects.Surfaces.Where(s => s.Role == AircraftControlSurfaceRole.Aileron).Count(s => s.Reversed), Is.EqualTo(1), "one aileron turns the other way");
            foreach (var surface in effects.Surfaces)
            {
                Assert.That(surface.Transform.GetComponentsInChildren<Collider>(), Is.Empty, $"'{surface.Transform.name}' does not block hits on the wings");
            }

            Assert.That(effects.VaporAnchors.Select(a => a.localPosition.x), Is.EqualTo(new[] { -AircraftFixtureBuilder.WingSpan / 2f, AircraftFixtureBuilder.WingSpan / 2f }));
            Assert.That(aircraft.GetComponent<AircraftStatusPublisher>(), Is.Not.Null);
        }

        [Test]
        public void ShootingTheLeftWingThroughItsHitboxBreaksItsLift()
        {
            CreateActors();

            var aircraft = Find(VehicleRangeSceneBuilder.AircraftName).GetComponent<Vehicle>();
            var damage = aircraft.GetComponent<AircraftPartDamage>();
            var wing = aircraft.transform.Find(AircraftFixtureBuilder.LeftWingName).GetComponent<Hitbox>();

            wing.ApplyResolvedHit(AircraftDamageSpec.DefaultWingDurability, null, null, DamageKind.Bullet);

            Assert.That(damage.Damage.IsBroken(AircraftDamageSpec.DefaultLeftWingId), Is.True);
            Assert.That(damage.Condition.LeftLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(Epsilon));
            Assert.That(damage.Condition.RightLift, Is.EqualTo(1f).Within(Epsilon));
            Assert.That(aircraft.Damageable.Health.Current, Is.EqualTo(VehicleSpec.DefaultMaxHealth - AircraftDamageSpec.DefaultWingDurability), "the hit counts at the part multiplier of 1");
        }

        [Test]
        public void APartTheAssetLeavesOutIsNotMarkedAndItsBoxHitsTheBody()
        {
            _aircraft.Damage.CopyFrom(new AircraftDamageSpec(new[]
            {
                new AircraftDamagePartSpec("left", AircraftDamageEffect.LeftLift, 100),
                new AircraftDamagePartSpec("right", AircraftDamageEffect.RightLift, 100),
            }));

            CreateActors();

            var aircraft = Find(VehicleRangeSceneBuilder.AircraftName).GetComponent<Vehicle>();
            Assert.That(aircraft.GetComponentsInChildren<VehiclePart>().Select(p => p.PartId), Is.EquivalentTo(new[] { "left", "right" }));
            Assert.That(aircraft.transform.Find(AircraftFixtureBuilder.TailName).GetComponent<VehiclePart>(), Is.Null);
            Assert.That(aircraft.Spec.Parts.Select(p => p.Id), Is.EquivalentTo(new[] { "left", "right" }));
        }

        private float GroundHeight(float x)
        {
            var origin = new Vector3(x, 20f, VehicleRangeSceneBuilder.GunCarOrigin.z);
            Assert.That(Physics.Raycast(origin, Vector3.down, out var hit, 40f), Is.True, $"nothing under x={x}");
            return hit.point.y;
        }

        private GameBootstrap CreateActors(AudioClip engine = null)
        {
            return VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules, _aircraft, _helicopter, _boat, _horse, _guided, engine);
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, spec);
            return weapon;
        }

        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail($"'{name}' is not in the scene");
            return null;
        }
    }
}
