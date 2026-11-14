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
    public sealed class ChargerBomberRangeFixtureTests
    {
        private Scene _scene;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void TheTpsRangeFindsBothEnemies()
        {
            var fixtures = TpsRangeSceneBuilder.Fixtures();

            Assert.That(fixtures.OfType<ChargerRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(fixtures.OfType<BomberRangeFixture>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheChargerWaitsInAWalledPenForTheBake()
        {
            AddBootstrap();

            var root = new ChargerRangeFixture().Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(ChargerRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.name, Is.EqualTo(ChargerRangeFixture.EnemyName));
            Assert.That(agent.Id, Is.EqualTo(ChargerRangeFixture.EnemyId));
            Assert.That(agent.Archetype, Is.EqualTo(EnemyArchetype.Charger));
            Assert.That(agent.SightRange, Is.EqualTo(ChargerRangeFixture.SightRange));
            Assert.That(agent.gameObject.activeSelf, Is.False, "it waits until the pen is baked");

            var charger = agent.GetComponent<EnemyCharger>();
            Assert.That(charger, Is.Not.Null);
            Assert.That(charger.Tell, Is.Null, "the tell is made at run time so a prefab set later is used");

            var bake = root.GetComponent<NavMeshBakeActivator>();
            Assert.That(bake.ActivateAfterBake, Has.Member(agent.gameObject));
            foreach (var wallName in ChargerRangeFixture.WallNames)
            {
                var wall = bake.Source.Find(wallName);
                Assert.That(wall, Is.Not.Null, wallName);
                Assert.That(wall.GetComponent<Collider>().bounds.max.y, Is.LessThan(EnemyAgent.DefaultEyeHeight), "the player can see and vault over it");
            }
        }

        [Test]
        public void TheChargeIsStoppedByThePenWallsAndTheChargerStandsInside()
        {
            var spec = ChargerRangeFixture.Spec();
            var footprint = new ChargerRangeFixture().Footprint;
            var ground = ChargerRangeFixture.EnemyGround;

            Assert.That(ground.x, Is.InRange(footprint.min.x, footprint.max.x));
            Assert.That(ground.z, Is.InRange(footprint.min.z, footprint.max.z));
            Assert.That(spec.Charger.ChargeDistance, Is.GreaterThan(ChargerRangeFixture.PenDepth), "a dodged charge reaches a wall");
            Assert.That(spec.Charger.TriggerDistance, Is.LessThan(spec.SightRange));

            // 突進の線を引く胴の高さ (足元から体の半分) が壁より下に来ると、壁を越えて囲いの外へ走り出る。
            Assert.That(
                ChargerRangeFixture.WallHeight,
                Is.GreaterThan((ChargerRangeFixture.EnemyBodySize.Height / 2f) - ChargerRangeFixture.EnemyBodySize.Radius));
        }

        [Test]
        public void TheBomberWaitsOnItsYardWithASignal()
        {
            AddBootstrap();

            var root = new BomberRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.name, Is.EqualTo(BomberRangeFixture.RootName));
            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.Id, Is.EqualTo(BomberRangeFixture.EnemyId));
            Assert.That(agent.Archetype, Is.EqualTo(EnemyArchetype.Bomber));
            Assert.That(agent.gameObject.activeSelf, Is.False);

            var bomber = agent.GetComponent<EnemyBomber>();
            Assert.That(bomber, Is.Not.Null);
            Assert.That(bomber.Signal, Is.Null, "the signal is made at run time so a prefab set later is used");
            Assert.That(root.GetComponent<NavMeshBakeActivator>().ActivateAfterBake, Has.Member(agent.gameObject));
        }

        // ほかの smoke の手順の間に襲ってこないよう、出現位置と開けた場所の中心は視界の外に置き、爆発も届かないようにする。
        [Test]
        public void NeitherEnemyCanSeeTheSpawnOrTheOpenArea()
        {
            var eye = Vector3.up * EnemyAgent.DefaultEyeHeight;
            foreach (var point in new[] { GameBootstrap.PlayerSpawn, TpsRangeSceneBuilder.OpenAreaCenter })
            {
                Assert.That(Vector3.Distance(ChargerRangeFixture.EnemyGround + eye, point), Is.GreaterThan(ChargerRangeFixture.SightRange));
                Assert.That(Vector3.Distance(BomberRangeFixture.EnemyGround + eye, point), Is.GreaterThan(BomberRangeFixture.SightRange));
                Assert.That(
                    Vector3.Distance(BomberRangeFixture.EnemyGround, point),
                    Is.GreaterThan(BomberRangeFixture.SightRange + BomberSpec.DefaultBlastRadius));
            }
        }

        [Test]
        public void WithoutAWeaponOnlyTheGroundIsPlaced()
        {
            LogAssert.Expect(LogType.Warning, "[ChargerRangeFixture] the range has no weapon for the charger; placing the pen only");
            var chargerRoot = new ChargerRangeFixture().Place(_scene, GameRules.Default);
            Assert.That(chargerRoot.GetComponentsInChildren<EnemyAgent>(true), Is.Empty);
            Assert.That(chargerRoot.transform.Find(ChargerRangeFixture.NavFloorName), Is.Not.Null);

            LogAssert.Expect(LogType.Warning, "[BomberRangeFixture] the range has no weapon for the bomber; placing the floor only");
            var bomberRoot = new BomberRangeFixture().Place(_scene, GameRules.Default);
            Assert.That(bomberRoot.GetComponentsInChildren<EnemyAgent>(true), Is.Empty);
        }

        private void AddBootstrap()
        {
            var bootstrapObject = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObject, _scene);
            bootstrapObject.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };
        }
    }
}
