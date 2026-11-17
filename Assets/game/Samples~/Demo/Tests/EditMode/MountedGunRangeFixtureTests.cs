using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class MountedGunRangeFixtureTests
    {
        private const float Epsilon = 1e-3f;
        private const string HeatGunId = "5d1b7e3a-9c24-4f60-8a1e-c3f2b6d4a789";

        private Scene _scene;
        private WeaponDefinition _rifle;
        private WeaponDefinition _heatGun;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _heatGun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _heatGun.name = "HeatGun";
            WeaponAssetBuilder.Configure(
                _heatGun,
                new WeaponSpec(
                    HeatGunId,
                    "weapon.heatGun",
                    WeaponCategory.AssaultRifle,
                    trigger: new TriggerSpec(FireMode.Auto, 0.12f),
                    ammo: new AmmoSpec(1, 0, 0f, kind: AmmoKind.Heat, heat: new HeatSpec(0.1f, 0.4f, 0.3f, 1.5f)),
                    impact: new ImpactSpec(22)));
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_heatGun);
        }

        [Test]
        public void TheRangeFindsTheYard()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<MountedGunRangeFixture>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheGunsUseTheAutomaticHeatWeapon()
        {
            Assert.That(MountedGunRangeFixture.PickGunWeapon(new[] { null, _rifle, _heatGun }), Is.SameAs(_heatGun));
            Assert.That(MountedGunRangeFixture.PickGunWeapon(new[] { _rifle }), Is.SameAs(_rifle), "without a heat weapon it still places a gun");
            Assert.That(MountedGunRangeFixture.PickGunWeapon(new WeaponDefinition[0]), Is.Null);
        }

        [Test]
        public void PlacesTwoShieldedGunsTargetsAndAGunnerWaitingForTheBake()
        {
            AddBootstrap();

            var root = new MountedGunRangeFixture().Place(_scene, new[] { _rifle, _heatGun });

            Assert.That(root.name, Is.EqualTo(MountedGunRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var guns = root.GetComponentsInChildren<MountedGun>();
            Assert.That(guns.Select(g => g.name), Is.EquivalentTo(new[] { MountedGunRangeFixture.PlayerGunName, MountedGunRangeFixture.EnemyGunName }));
            foreach (var gun in guns)
            {
                Assert.That(gun.IsReady, Is.True);
                Assert.That(gun.Vehicle.Seats.Count, Is.EqualTo(1));
                Assert.That(gun.Vehicle.Spec.DriverSeatIndex, Is.EqualTo(-1));
                Assert.That(gun.Vehicle.GetComponent<IVehicleMotor>(), Is.Null);
                Assert.That(gun.Mount.WeaponSpec.Id, Is.EqualTo(HeatGunId));
                Assert.That(gun.Mount.Spec.YawMaxDegrees, Is.EqualTo(MountedGunFixtureBuilder.YawLimitDegrees));
                Assert.That(gun.Shield, Is.Not.Null);
                Assert.That(gun.Shield.transform.parent, Is.SameAs(gun.Mount.transform), "the shield is saved with the scene");
                Assert.That(gun.Mount.transform.Find(MountedGunFixtureBuilder.BarrelName).GetComponent<Collider>(), Is.Null);
            }

            var playerGun = guns.Single(g => g.name == MountedGunRangeFixture.PlayerGunName);
            Assert.That(Vector3.Distance(playerGun.transform.position, MountedGunRangeFixture.PlayerGunOrigin), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(playerGun.transform.forward, Vector3.left), Is.LessThan(0.1f));

            var targets = root.GetComponentsInChildren<TargetDummy>();
            Assert.That(targets.Length, Is.EqualTo(MountedGunRangeFixture.TargetCount));
            foreach (var target in targets)
            {
                Assert.That(playerGun.CanAimAt(target.Damageable.AimPoint), Is.True, $"{target.Damageable.Id} is in front of the player's gun");
            }

            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.Id, Is.EqualTo(MountedGunRangeFixture.EnemyId));
            Assert.That(agent.gameObject.activeSelf, Is.False, "it waits until the floor is baked");
            Assert.That(agent.SightRange, Is.EqualTo(MountedGunRangeFixture.SightRange));
            Assert.That(agent.WeaponDefinition, Is.SameAs(_rifle));
            Assert.That(agent.GetComponent<EnemyMountedGunner>(), Is.Not.Null);
            Assert.That(root.GetComponent<NavMeshBakeActivator>().ActivateAfterBake, Has.Member(agent.gameObject));
        }

        [Test]
        public void TheGunnerStartsNearItsGunButCannotSeeThePlayersGun()
        {
            var enemyGunBoard = MountedGunRangeFixture.EnemyGunOrigin
                + (MountedGunRangeFixture.EnemyGunRotation * MountedGunFixtureBuilder.EnterLocal);
            var toGun = Flat(enemyGunBoard - MountedGunRangeFixture.EnemyGround).magnitude;

            Assert.That(toGun, Is.LessThan(MountedGunUseSpec.DefaultSeekRadius));
            Assert.That(toGun, Is.GreaterThan(VehicleSpec.DefaultEnterRange), "it walks a few steps to board");
            Assert.That(
                Flat(MountedGunRangeFixture.PlayerGunOrigin - MountedGunRangeFixture.EnemyGround).magnitude,
                Is.GreaterThan(MountedGunRangeFixture.SightRange),
                "trying the heat at the player's gun does not wake the gunner");

            var floorHalf = MountedGunRangeFixture.NavFloorSize / 2f;
            Assert.That(Mathf.Abs(enemyGunBoard.x - MountedGunRangeFixture.NavFloorCenter.x), Is.LessThan(floorHalf));
            Assert.That(Mathf.Abs(enemyGunBoard.z - MountedGunRangeFixture.NavFloorCenter.z), Is.LessThan(floorHalf));
        }

        [Test]
        public void WithoutAnyWeaponOnlyTheTargetsArePlaced()
        {
            LogAssert.Expect(LogType.Warning, "[MountedGunRangeFixture] the range has no weapon for the mounted guns; placing the targets only");

            var root = new MountedGunRangeFixture().Place(_scene, new WeaponDefinition[0]);

            Assert.That(root.GetComponentsInChildren<MountedGun>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<TargetDummy>().Length, Is.EqualTo(MountedGunRangeFixture.TargetCount));
        }

        [Test]
        public void TheFootprintCoversBothGunsTheTargetsAndTheFloor()
        {
            var footprint = new MountedGunRangeFixture().Footprint;

            Assert.That(footprint.Contains(MountedGunRangeFixture.PlayerGunOrigin + Vector3.up), Is.True);
            Assert.That(footprint.Contains(MountedGunRangeFixture.EnemyGunOrigin + Vector3.up), Is.True);
            Assert.That(footprint.Contains(MountedGunRangeFixture.EnemyGround + Vector3.up), Is.True);
            for (var i = 0; i < MountedGunRangeFixture.TargetCount; i++)
            {
                Assert.That(footprint.Contains(MountedGunRangeFixture.TargetGround(i) + Vector3.up), Is.True);
            }

            Assert.That(footprint.min.y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(footprint.min.x, Is.GreaterThan(-TpsRangeSceneBuilder.FloorHalfExtent));
        }

        private void AddBootstrap()
        {
            var bootstrapObject = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObject, _scene);
            bootstrapObject.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
