using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class DecoyRangeFixtureTests
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
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<DecoyRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<DecoySmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheEnemyWaitsAsleepOnABakedFloor()
        {
            var bootstrap = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrap, _scene);
            bootstrap.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };

            var root = new DecoyRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.name, Is.EqualTo(DecoyRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            var agent = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(agent.name, Is.EqualTo(DecoyRangeFixture.EnemyName));
            Assert.That(agent.Id, Is.EqualTo(DecoyRangeFixture.EnemyId));
            Assert.That(agent.gameObject.activeSelf, Is.False, "the smoke step wakes it on its turn");
            Assert.That(agent.GetComponent<SideMember>(), Is.Null, "it targets the tagged player, so the player's decoy is hostile to it");
            Assert.That(agent.GetComponent<EnemyStealth>(), Is.Not.Null, "the awareness meter fills on the decoy");

            var bake = root.GetComponent<NavMeshBakeActivator>();
            Assert.That(bake, Is.Not.Null);
            Assert.That(bake.Source.name, Is.EqualTo(DecoyRangeFixture.NavFloorName));
            Assert.That(bake.ActivateAfterBake, Is.Empty, "the bake must not wake the enemy");
        }

        [Test]
        public void TheDecoyIsInSightOfTheEnemyOnItsFloorAndItsCallReachesNoOtherEnemy()
        {
            var footprint = new DecoyRangeFixture().Footprint;
            foreach (var point in new[] { DecoyRangeFixture.PlayerGround, DecoyRangeFixture.DecoyGround, DecoyRangeFixture.EnemyGround })
            {
                Assert.That(point.x, Is.InRange(footprint.min.x, footprint.max.x), point.ToString());
                Assert.That(point.z, Is.InRange(footprint.min.z, footprint.max.z), point.ToString());
            }

            var decoy = DecoyRangeFixture.DecoyGround;
            Assert.That(Vector3.Distance(decoy, DecoyRangeFixture.EnemyGround), Is.LessThan(EnemySpec.DefaultSightRange));
            Assert.That(Vector3.Distance(decoy, DecoyRangeFixture.EnemyGround), Is.LessThan(EnemySpec.DefaultAttackRange));

            // 敵が囮のあった場所まで歩けるよう、囮の足元も敵の足元も NavMesh の床の上にある。
            var floorMin = DecoyRangeFixture.NavFloorCenter - new Vector3(DecoyRangeFixture.NavFloorSize.x / 2f, 0f, DecoyRangeFixture.NavFloorSize.y / 2f);
            var floorMax = DecoyRangeFixture.NavFloorCenter + new Vector3(DecoyRangeFixture.NavFloorSize.x / 2f, 0f, DecoyRangeFixture.NavFloorSize.y / 2f);
            foreach (var point in new[] { decoy, DecoyRangeFixture.EnemyGround })
            {
                Assert.That(point.x, Is.InRange(floorMin.x, floorMax.x), point.ToString());
                Assert.That(point.z, Is.InRange(floorMin.z, floorMax.z), point.ToString());
            }

            // 置いたプレイヤーは出現位置へ戻るので、囮が壊れた後に敵から見えない。
            Assert.That(Vector3.Distance(GameBootstrap.PlayerSpawn, DecoyRangeFixture.EnemyGround), Is.GreaterThan(EnemySpec.DefaultSightRange));

            foreach (var other in new[]
            {
                ChargerRangeFixture.EnemyGround,
                BomberRangeFixture.EnemyGround,
                ShieldEnemyRangeFixture.EnemyGround,
                MeleeEnemyRangeFixture.EnemyGround,
                StaggerRangeFixture.EnemyFeet,
                SquadCommandRangeFixture.BotPoint,
                StealthRangeFixture.EnemyFeet,
                TurretRangeFixture.PreyGround,
                TurretRangeFixture.RaiderGround,
            })
            {
                Assert.That(Vector3.Distance(decoy, other), Is.GreaterThan(DecoySpec.DefaultCallRadius), other.ToString());
            }

            foreach (var perch in SniperRangeFixture.Perches())
            {
                Assert.That(Vector3.Distance(decoy, perch), Is.GreaterThan(DecoySpec.DefaultCallRadius));
            }
        }
    }
}
