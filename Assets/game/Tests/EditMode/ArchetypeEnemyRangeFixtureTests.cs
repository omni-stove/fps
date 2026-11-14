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
    public sealed class ArchetypeEnemyRangeFixtureTests
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

            Assert.That(fixtures.OfType<ShieldEnemyRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(fixtures.OfType<SniperRangeFixture>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheShieldEnemyStandsReadyWithAShieldAndWaitsForTheBake()
        {
            AddBootstrap();

            var root = new ShieldEnemyRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.name, Is.EqualTo(ShieldEnemyRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.name, Is.EqualTo(ShieldEnemyRangeFixture.EnemyName));
            Assert.That(agent.Id, Is.EqualTo(ShieldEnemyRangeFixture.EnemyId));
            Assert.That(agent.gameObject.activeSelf, Is.False, "it waits until the floor is baked");
            Assert.That(agent.Archetype, Is.EqualTo(EnemyArchetype.Shield));
            Assert.That(agent.SightRange, Is.EqualTo(ShieldEnemyRangeFixture.SightRange));
            Assert.That(agent.WeaponDefinition, Is.SameAs(_rifle));

            var shield = agent.GetComponent<EnemyShield>();
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.Plate, Is.Not.Null);
            Assert.That(shield.Plate.transform.parent, Is.SameAs(agent.transform));
            Assert.That(shield.Plate.GetComponent<Collider>(), Is.Not.Null);

            var bake = root.GetComponent<NavMeshBakeActivator>();
            Assert.That(bake.ActivateAfterBake, Has.Member(agent.gameObject));
        }

        [Test]
        public void TheSniperStandsOnItsDeckWithPerchesAndABeam()
        {
            AddBootstrap();

            var root = new SniperRangeFixture().Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(SniperRangeFixture.RootName));
            var deck = root.transform.Find(SniperRangeFixture.DeckName);
            Assert.That(deck, Is.Not.Null);
            Assert.That(deck.GetComponent<Collider>().bounds.max.y, Is.EqualTo(SniperRangeFixture.DeckHeight).Within(1e-4f));

            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(agent.gameObject.activeSelf, Is.False);
            Assert.That(Vector3.Distance(agent.GroundPosition, SniperRangeFixture.Perches()[1]), Is.LessThan(1e-3f));

            var sniper = agent.GetComponent<EnemySniper>();
            Assert.That(sniper, Is.Not.Null);
            Assert.That(sniper.Perches, Is.EqualTo(SniperRangeFixture.Perches()));
            Assert.That(sniper.AimBeam, Is.Not.Null);
            Assert.That(sniper.AimBeam.IsChildOf(agent.transform), Is.False, "the beam does not turn with the sniper");
            Assert.That(sniper.AimBeam.GetComponent<Collider>(), Is.Null, "the beam does not stop bullets or sight");
            Assert.That(sniper.AimBeam.gameObject.activeSelf, Is.False);

            Assert.That(root.GetComponent<NavMeshBakeActivator>().Source, Is.SameAs(deck));
        }

        [Test]
        public void ThePerchesAreOnTheDeckTop()
        {
            var footprint = new SniperRangeFixture().Footprint;
            foreach (var perch in SniperRangeFixture.Perches())
            {
                Assert.That(perch.y, Is.EqualTo(SniperRangeFixture.DeckHeight));
                Assert.That(perch.x, Is.InRange(footprint.min.x, footprint.max.x));
                Assert.That(perch.z, Is.InRange(footprint.min.z, footprint.max.z));
            }
        }

        // ほかの smoke の手順の間に撃ってこないよう、出現位置と開けた場所の中心は視界の外に置く。
        [Test]
        public void NeitherEnemyCanSeeTheSpawnOrTheOpenArea()
        {
            var eye = Vector3.up * EnemyAgent.DefaultEyeHeight;
            foreach (var point in new[] { GameBootstrap.PlayerSpawn, TpsRangeSceneBuilder.OpenAreaCenter })
            {
                Assert.That(Vector3.Distance(ShieldEnemyRangeFixture.EnemyGround + eye, point), Is.GreaterThan(ShieldEnemyRangeFixture.SightRange));
                foreach (var perch in SniperRangeFixture.Perches())
                {
                    Assert.That(Vector3.Distance(perch + eye, point), Is.GreaterThan(SniperRangeFixture.SightRange));
                }
            }
        }

        [Test]
        public void WithoutAWeaponOnlyTheGroundIsPlaced()
        {
            LogAssert.Expect(LogType.Warning, "[ShieldEnemyRangeFixture] the range has no weapon for the shield enemy; placing the floor only");
            var shieldRoot = new ShieldEnemyRangeFixture().Place(_scene, GameRules.Default);
            Assert.That(shieldRoot.GetComponentsInChildren<EnemyAgent>(true), Is.Empty);

            LogAssert.Expect(LogType.Warning, "[SniperRangeFixture] the range has no weapon for the sniper; placing the deck only");
            var sniperRoot = new SniperRangeFixture().Place(_scene, GameRules.Default);
            Assert.That(sniperRoot.GetComponentsInChildren<EnemyAgent>(true), Is.Empty);
            Assert.That(sniperRoot.transform.Find(SniperRangeFixture.DeckName), Is.Not.Null);
        }

        private void AddBootstrap()
        {
            var bootstrapObject = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObject, _scene);
            bootstrapObject.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };
        }
    }
}
