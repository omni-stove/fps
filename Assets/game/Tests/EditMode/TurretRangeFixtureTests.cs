using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class TurretRangeFixtureTests
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
        public void TheTpsRangeFindsTheYardAndItsSmokeStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<TurretRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<TurretSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheEnemiesWaitAsleepOnABakedFloor()
        {
            var bootstrap = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrap, _scene);
            bootstrap.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };

            var root = new TurretRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.name, Is.EqualTo(TurretRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            var agents = root.GetComponentsInChildren<EnemyAgent>(true);
            Assert.That(agents.Select(a => a.name), Is.EquivalentTo(new[] { TurretRangeFixture.PreyName, TurretRangeFixture.RaiderName }));
            Assert.That(agents.Select(a => a.Id), Is.EquivalentTo(new[] { TurretRangeFixture.PreyId, TurretRangeFixture.RaiderId }));
            Assert.That(agents, Has.All.Matches<EnemyAgent>(a => !a.gameObject.activeSelf), "the smoke step wakes them on its turn");
            Assert.That(agents, Has.All.Matches<EnemyAgent>(a => a.GetComponent<SideMember>() == null), "they target the tagged player and the turret");

            var raider = agents.Single(a => a.name == TurretRangeFixture.RaiderName);
            Assert.That(raider.GetComponent<Damageable>().MaxHealth, Is.EqualTo(TurretRangeFixture.RaiderMaxHealth));

            var bake = root.GetComponent<NavMeshBakeActivator>();
            Assert.That(bake, Is.Not.Null);
            Assert.That(bake.Source.name, Is.EqualTo(TurretRangeFixture.NavFloorName));
            Assert.That(bake.ActivateAfterBake, Is.Empty, "the bake must not wake the enemies");
        }

        [Test]
        public void TheTurretAndTheEnemiesFitTheYardAndTheTurretSeesNoOtherEnemy()
        {
            var footprint = new TurretRangeFixture().Footprint;
            foreach (var point in new[]
            {
                TurretRangeFixture.PlayerGround,
                TurretRangeFixture.TurretGround,
                TurretRangeFixture.PreyGround,
                TurretRangeFixture.RaiderGround,
            })
            {
                Assert.That(point.x, Is.InRange(footprint.min.x, footprint.max.x), point.ToString());
                Assert.That(point.z, Is.InRange(footprint.min.z, footprint.max.z), point.ToString());
            }

            var turret = TurretRangeFixture.TurretGround;
            Assert.That(Vector3.Distance(turret, TurretRangeFixture.PreyGround), Is.LessThan(TurretSpec.DefaultSightRange));
            Assert.That(Vector3.Distance(turret, TurretRangeFixture.RaiderGround), Is.LessThan(TurretSpec.DefaultSightRange));

            // 敵は、タレットがプレイヤーより近いときにタレットを狙う。
            Assert.That(
                Vector3.Distance(TurretRangeFixture.RaiderGround, turret),
                Is.LessThan(Vector3.Distance(TurretRangeFixture.RaiderGround, TurretRangeFixture.PlayerGround)));

            foreach (var other in new[]
            {
                ChargerRangeFixture.EnemyGround,
                BomberRangeFixture.EnemyGround,
                ShieldEnemyRangeFixture.EnemyGround,
                MeleeEnemyRangeFixture.EnemyGround,
                StaggerRangeFixture.EnemyFeet,
                SquadCommandRangeFixture.BotPoint,
            })
            {
                Assert.That(Vector3.Distance(turret, other), Is.GreaterThan(TurretSpec.DefaultSightRange), other.ToString());
            }

            foreach (var perch in SniperRangeFixture.Perches())
            {
                Assert.That(Vector3.Distance(turret, perch), Is.GreaterThan(TurretSpec.DefaultSightRange));
            }
        }
    }
}
