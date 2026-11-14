using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class StaggerRangeFixtureTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheRangeFindsTheFixtureAndTheSmokeStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<StaggerRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<StaggerSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesAnInactiveEnemyThatWakesUpAfterTheNavMeshIsBaked()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(WeaponAssetBuilder.CatalogPath), Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");
            var fixture = new StaggerRangeFixture();

            var root = fixture.Place(_scene, GameRulesAssetBuilder.TpsRangeRules());

            Assert.That(root.name, Is.EqualTo(StaggerRangeFixture.FixtureName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var enemy = root.GetComponentInChildren<EnemyAgent>(true);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.gameObject.activeSelf, Is.False, "the NavMeshAgent must not look for a NavMesh before it is baked");
            Assert.That(root.GetComponent<NavMeshBakeActivator>().ActivateAfterBake, Is.EqualTo(new[] { enemy.gameObject }));
            Assert.That(enemy.GetComponent<StaggerReaction>(), Is.Not.Null);
            Assert.That(enemy.SightRange, Is.EqualTo(StaggerRangeFixture.EnemySightRange));
            Assert.That(enemy.AttackRange, Is.EqualTo(StaggerRangeFixture.EnemyAttackRange));
            Assert.That(enemy.PatrolRoute, Is.Empty);

            var head = enemy.transform.Find(StaggerRangeFixture.HeadName).GetComponent<Hitbox>();
            var legs = enemy.transform.Find(StaggerRangeFixture.LegsName).GetComponent<Hitbox>();
            Assert.That(head.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(legs.Zone, Is.EqualTo(HitZone.Limb));
            Assert.That(head.Owner, Is.SameAs(enemy.GetComponent<Damageable>()));
            Assert.That(legs.Owner, Is.SameAs(enemy.GetComponent<Damageable>()));

            var pad = root.transform.Find(StaggerRangeFixture.PadName).GetComponent<BoxCollider>();
            Assert.That(pad, Is.Not.Null);
            Assert.That(fixture.Footprint.Contains(pad.transform.position), Is.True);
        }

        [Test]
        public void TheStandPointIsOffThePadAndInsideTheEnemysSight()
        {
            var fixture = new StaggerRangeFixture();
            var stand = StaggerRangeFixture.StandPoint;
            var flatFootprint = fixture.Footprint;
            flatFootprint.center = new Vector3(flatFootprint.center.x, stand.y, flatFootprint.center.z);

            Assert.That(flatFootprint.Contains(stand), Is.False);
            Assert.That(Vector3.Distance(stand, StaggerRangeFixture.Center), Is.LessThan(StaggerRangeFixture.EnemyAttackRange));
            Assert.That(Vector3.Distance(GameBootstrap.PlayerSpawn, StaggerRangeFixture.Center), Is.GreaterThan(StaggerRangeFixture.EnemySightRange), "the enemy cannot see the spawn");
        }
    }
}
