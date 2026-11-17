using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class VehicleRangeSceneBuilderTests
    {
        private const float Epsilon = 1e-3f;
        private const string HeatGunId = "0f6a2c1e-7b3d-4e59-9a84-d2c5b1e3f760";

        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        private Scene _scene;
        private WeaponDefinition _rifle;
        private WeaponDefinition _heatGun;
        private GameRulesDefinition _rules;

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
            _rules = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _rules.name = "Rules";
            _assets.Add(_rules);
            GameRulesAssetBuilder.Configure(_rules, GameRulesAssetBuilder.TpsRangeRules());
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
            Assert.That(VehicleRangeSceneBuilder.MenuPath, Is.EqualTo("Fps/Build Vehicle Range Scene"));
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
            var bootstrap = VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules);

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
            VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules);

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
            VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules);

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
        public void TheEnemyStandsBeyondEarshotOfWhatTheSmokeFires()
        {
            VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, _rules);

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
        public void ActorsNeedWeaponsAndRules()
        {
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, null, _heatGun, _rules));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, null, _rules));
            Assert.Throws<ArgumentNullException>(() => VehicleRangeSceneBuilder.CreateActors(_scene, new[] { _rifle }, _heatGun, null));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed when an argument is missing");
        }

        private float GroundHeight(float x)
        {
            var origin = new Vector3(x, 20f, VehicleRangeSceneBuilder.GunCarOrigin.z);
            Assert.That(Physics.Raycast(origin, Vector3.down, out var hit, 40f), Is.True, $"nothing under x={x}");
            return hit.point.y;
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
