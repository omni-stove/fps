using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class DebrisCrushRangeFixtureTests
    {
        private Scene _scene;
        private GameObject _poolObject;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_poolObject != null)
            {
                Object.DestroyImmediate(_poolObject);
            }

            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheRangeAndItsSmokeFindTheFixtureAndTheStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<DebrisCrushRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<DebrisCrushSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesACrushingPillarAndAnEnemyUnderItsBeam()
        {
            var root = new DebrisCrushRangeFixture().Place(_scene, GameRulesAssetBuilder.TpsRangeRules());
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(DebrisCrushRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var pillar = root.GetComponentInChildren<DestructibleStructure>();
            Assert.That(pillar.name, Is.EqualTo(DebrisCrushRangeFixture.PillarName));
            Assert.That(pillar.DestructionEnabled, Is.True, "the TPS range rules turn destruction on");
            Assert.That(pillar.DebrisCrushEnabled, Is.True);
            Assert.That(pillar.Pieces, Has.Count.EqualTo(DebrisCrushRangeFixture.PillarHeight + DebrisCrushRangeFixture.BeamLength));
            Assert.That(pillar.Pieces.Count(p => p.Grounded), Is.EqualTo(1));
            Assert.That(pillar.Pieces[DebrisCrushRangeFixture.BaseIndex].Grounded, Is.True);

            var enemy = root.GetComponentsInChildren<Damageable>().Single();
            Assert.That(enemy.name, Is.EqualTo(DebrisCrushRangeFixture.EnemyName));
            Assert.That(enemy.Id, Is.EqualTo(DebrisCrushRangeFixture.EnemyId));
            Assert.That(enemy.MaxHealth, Is.EqualTo(DebrisCrushRangeFixture.EnemyMaxHealth));
            Assert.That(enemy.GetComponent<Hitbox>(), Is.Not.Null);
            Assert.That(enemy.TryGetBounds(out var body), Is.True);
            Assert.That(body.min.y, Is.EqualTo(0f).Within(1e-4f), "the enemy stands on the floor");

            var beam = pillar.Pieces[DebrisCrushRangeFixture.PillarHeight + DebrisCrushRangeFixture.EnemyUnderBeam - 1].Collider.bounds;
            Assert.That(beam.min.y, Is.GreaterThan(body.max.y), "the beam hangs above the enemy");
            Assert.That(enemy.transform.position.x, Is.InRange(beam.min.x, beam.max.x), "the enemy is right under the beam");

            var footprint = new DebrisCrushRangeFixture().Footprint;
            foreach (var piece in pillar.Pieces)
            {
                AssertInsideOnTheFloor(footprint, piece.Collider.bounds, piece.name);
            }

            AssertInsideOnTheFloor(footprint, body, enemy.name);
        }

        [Test]
        public void ThePillarStaysWholeWhenTheRulesTurnDestructionOff()
        {
            var rules = GameRulesAssetBuilder.TpsRangeRules().WithDestruction(Destruction.Off);
            var root = new DebrisCrushRangeFixture().Place(_scene, rules);

            Assert.That(root.GetComponentInChildren<DestructibleStructure>().DestructionEnabled, Is.False);
        }

        [Test]
        public void BreakingTheBaseDropsCrushingDebrisFromEveryPiece()
        {
            var root = new DebrisCrushRangeFixture().Place(_scene, GameRulesAssetBuilder.TpsRangeRules());
            var pillar = root.GetComponentInChildren<DestructibleStructure>();
            _poolObject = new GameObject("TestDebrisPool");
            var pool = _poolObject.AddComponent<DebrisPool>();
            pillar.DebrisPool = pool;
            var breaker = new GameObject("Breaker");
            breaker.transform.SetParent(_poolObject.transform);

            var basePiece = pillar.Pieces[DebrisCrushRangeFixture.BaseIndex];
            using (CombatEvents.Attribute(breaker, "rifle"))
            {
                pillar.ApplyStructureHit(basePiece.Collider, basePiece.transform.position, basePiece.MaxHealth);
            }

            for (var i = 0; i < pillar.Pieces.Count; i++)
            {
                Assert.That(pillar.Integrity.IsDestroyed(i), Is.True, $"piece {i}");
            }

            var crushers = pool.GetComponentsInChildren<DebrisCrusher>();
            Assert.That(crushers, Has.Length.EqualTo(pool.Count));
            Assert.That(crushers.All(c => c.Attribution.HasValue && c.Attribution.Value.KillerId == CombatEvents.OfflineId(breaker)), Is.True);
        }

        private static void AssertInsideOnTheFloor(Bounds footprint, Bounds bounds, string name)
        {
            const float epsilon = 1e-3f;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(footprint.min.x - epsilon), name);
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(footprint.max.x + epsilon), name);
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(footprint.min.z - epsilon), name);
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(footprint.max.z + epsilon), name);
        }
    }
}
