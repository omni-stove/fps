using System;
using System.Collections.Generic;
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
    public sealed class StealthRangeFixtureTests
    {
        private Scene _scene;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

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
        public void TheRangeFindsTheFixtureAndTheSmokeFindsTheStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<StealthRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<StealthSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesAnInactiveEnemyThatWakesUpOnceTheNavMeshIsBuilt()
        {
            var rifle = NewWeapon();
            TpsRangeSceneBuilder.PopulateScene(
                _scene, new[] { rifle }, NewRules(), new ITpsRangeFixture[] { new StealthRangeFixture() });

            var area = _scene.GetRootGameObjects().Single(r => r.name == StealthRangeFixture.AreaName);
            var builder = area.GetComponent<RuntimeNavMeshArea>();
            Assert.That(builder, Is.Not.Null);
            Assert.That(builder.Size, Is.EqualTo(StealthRangeFixture.AreaSize));
            Assert.That(builder.AgentTypeId, Is.EqualTo(SceneBuildHelpers.HumanoidAgentTypeId));

            var enemy = area.GetComponentInChildren<EnemyAgent>(true);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.gameObject.activeSelf, Is.False, "it must wait for the NavMesh");
            Assert.That(builder.ActivateAfterBuild, Is.EqualTo(new[] { enemy.gameObject }));
            Assert.That(enemy.name, Is.EqualTo(StealthRangeFixture.EnemyName));
            Assert.That(enemy.Id, Is.EqualTo(StealthRangeFixture.EnemyId));
            Assert.That(enemy.WeaponDefinition, Is.SameAs(rifle));
            Assert.That(enemy.GetComponent<EnemyStealth>(), Is.Not.Null);
            Assert.That(enemy.GroundPosition.x, Is.EqualTo(StealthRangeFixture.EnemyFeet.x).Within(1e-4f));
            Assert.That(enemy.GroundPosition.z, Is.EqualTo(StealthRangeFixture.EnemyFeet.z).Within(1e-4f));

            var wall = area.transform.Find(StealthRangeFixture.WallName);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [Test]
        public void EverythingIsInsideTheAreaAndTheFootprint()
        {
            var fixture = new StealthRangeFixture();
            var area = new Bounds(StealthRangeFixture.AreaCenter, StealthRangeFixture.AreaSize);
            var footprint = fixture.Footprint;

            foreach (var feet in new[] { StealthRangeFixture.EnemyFeet, StealthRangeFixture.ApproachStart })
            {
                Assert.That(area.Contains(feet), Is.True, feet.ToString());
                Assert.That(Flat(footprint).Contains(Flat(feet)), Is.True, feet.ToString());
            }

            var wall = new Bounds(StealthRangeFixture.WallCenter, StealthRangeFixture.WallSize);
            Assert.That(area.Contains(wall.min) && area.Contains(wall.max), Is.True);
        }

        [Test]
        public void TheApproachStartsInSightButEverythingElseInTheRangeIsOutOfSight()
        {
            var sight = EnemySpec.DefaultSightRange;
            var enemy = StealthRangeFixture.EnemyFeet;

            var approach = Vector3.Distance(enemy, StealthRangeFixture.ApproachStart);
            Assert.That(approach, Is.LessThan(sight));
            Assert.That(approach, Is.GreaterThan(AwarenessSpec.DefaultFullGainDistance * 3f), "far enough for the meter to fill slowly");

            // ほかの手順でプレイヤーが立つ所から見えると、組み込みの手順の途中で撃たれる。
            var elsewhere = new[]
            {
                new Vector3(GameBootstrap.PlayerSpawn.x, 0f, GameBootstrap.PlayerSpawn.z),
                TpsRangeSceneBuilder.VaultWall.Center,
                TpsRangeSceneBuilder.OpenAreaCenter,
                TpsRangeSceneBuilder.HutCenter,
            };
            foreach (var point in elsewhere)
            {
                Assert.That(Vector3.Distance(Flat(enemy), Flat(point)), Is.GreaterThan(sight), point.ToString());
            }
        }

        [Test]
        public void PlacingWithoutAGameBootstrapPlacesNothing()
        {
            Assert.Throws<InvalidOperationException>(() => new StealthRangeFixture().Place(_scene, GameRules.Default));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Bounds Flat(Bounds b) => new Bounds(Flat(b.center), new Vector3(b.size.x, 1f, b.size.z));

        private WeaponDefinition NewWeapon()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, WeaponAssetBuilder.RifleSpec());
            return weapon;
        }

        private GameRulesDefinition NewRules()
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, GameRulesAssetBuilder.TpsRangeRules());
            return definition;
        }
    }
}
