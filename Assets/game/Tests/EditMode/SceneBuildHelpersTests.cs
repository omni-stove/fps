using System;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// シーン生成スクリプトの共通部分。既存のビルダー (EnemyTestSceneBuilder・MissionTestSceneBuilder) の出力が
    /// 切り出しの前後で変わらないことは、それぞれのテストで確かめている。ここでは共通部分そのものを確かめる。
    /// </summary>
    public sealed class SceneBuildHelpersTests
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
            EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void SetSurfaceMaterialAddsOnceAndOverwritesTheId()
        {
            var box = SceneBuildHelpers.CreateStaticBox(_scene, null, "Box", Vector3.zero, Vector3.one);

            var first = SceneBuildHelpers.SetSurfaceMaterial(box, "metal");
            var second = SceneBuildHelpers.SetSurfaceMaterial(box, " wood ");

            Assert.That(second, Is.SameAs(first));
            Assert.That(box.GetComponents<SurfaceMaterial>(), Has.Length.EqualTo(1));
            Assert.That(SurfaceMaterial.IdOf(box.GetComponent<Collider>()), Is.EqualTo("wood"));
            Assert.Throws<ArgumentNullException>(() => SceneBuildHelpers.SetSurfaceMaterial(null, "metal"));
        }

        [Test]
        public void SurfaceIdOfFallsBackToTheDefaultMaterial()
        {
            var ids = new System.Collections.Generic.Dictionary<string, string> { ["Wall"] = "concrete" };

            Assert.That(SceneBuildHelpers.SurfaceIdOf(ids, "Wall"), Is.EqualTo("concrete"));
            Assert.That(SceneBuildHelpers.SurfaceIdOf(ids, "Door"), Is.EqualTo(SurfaceMaterial.DefaultId));
            Assert.That(SceneBuildHelpers.SurfaceIdOf(ids, null), Is.EqualTo(SurfaceMaterial.DefaultId));
            Assert.That(SceneBuildHelpers.SurfaceIdOf(null, "Wall"), Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void BuildersShareTheSameBuildSettingsAndPathRules()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(BootstrapSceneBuilder.DefaultScenePath, true),
                new EditorBuildSettingsScene(CampaignSceneBuilder.ScenePath, false),
            };

            var shared = SceneBuildHelpers.WithSceneAppended(current, CampaignSceneBuilder.ScenePath);
            var enemy = EnemyTestSceneBuilder.WithSceneAppended(current, CampaignSceneBuilder.ScenePath);
            var mission = MissionTestSceneBuilder.WithSceneAppended(current, CampaignSceneBuilder.ScenePath);

            Assert.That(shared, Has.Length.EqualTo(2));
            Assert.That(shared[1].enabled, Is.True);
            for (var i = 0; i < shared.Length; i++)
            {
                Assert.That(enemy[i].path, Is.EqualTo(shared[i].path));
                Assert.That(enemy[i].enabled, Is.EqualTo(shared[i].enabled));
                Assert.That(mission[i].path, Is.EqualTo(shared[i].path));
                Assert.That(mission[i].enabled, Is.EqualTo(shared[i].enabled));
            }

            Assert.That(SceneBuildHelpers.ValidateAssetPath(@"Assets\game\Missions\X.asset "), Is.EqualTo("Assets/game/Missions/X.asset"));
            Assert.Throws<ArgumentException>(() => SceneBuildHelpers.ValidateAssetPath("Assets/../X.asset"));
        }

        [Test]
        public void AreaPadTriggerStandsOnTheFloor()
        {
            var pad = SceneBuildHelpers.CreateAreaPad("Pad", new Vector3(3f, 0f, 4f), new Vector3(6f, 0.1f, 4f), 2.5f);
            try
            {
                var collider = pad.GetComponent<BoxCollider>();
                var bottom = pad.transform.TransformPoint(collider.center - new Vector3(0f, collider.size.y / 2f, 0f));
                var top = pad.transform.TransformPoint(collider.center + new Vector3(0f, collider.size.y / 2f, 0f));

                Assert.That(collider.isTrigger, Is.True);
                Assert.That(pad.GetComponent<Rigidbody>().isKinematic, Is.True);
                Assert.That(bottom.y, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(top.y, Is.EqualTo(2.5f).Within(1e-3f));
                Assert.That(bottom.x, Is.EqualTo(3f).Within(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(pad);
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => SceneBuildHelpers.CreateAreaPad("Pad", Vector3.zero, new Vector3(1f, 0.1f, 1f), 0.05f));
        }

        [Test]
        public void EnemyStandsOnItsFeetUnderAParent()
        {
            var parent = new GameObject("Enemies");
            SceneManager.MoveGameObjectToScene(parent, _scene);
            var feet = new Vector3(2f, 0f, 5f);
            var rotation = SceneBuildHelpers.FacingTowards(feet, feet + Vector3.left);

            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var agent = SceneBuildHelpers.CreateEnemy(
                    _scene,
                    parent.transform,
                    "Enemy",
                    "enemy",
                    feet,
                    rotation,
                    Array.Empty<Vector3>(),
                    new EnemyBody(2f, 0.5f, 3.5f, 80),
                    rifle);

                AssertStandsOnItsFeet(agent, parent.transform, feet);
                Assert.That(agent.WeaponDefinition, Is.SameAs(rifle));
            }
            finally
            {
                Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void EnemyNeedsAWeaponDefinition()
        {
            Assert.Throws<ArgumentNullException>(
                () => SceneBuildHelpers.CreateEnemy(
                    _scene, null, "Enemy", "enemy", Vector3.zero, Quaternion.identity, Array.Empty<Vector3>(),
                    new EnemyBody(2f, 0.5f, 3.5f, 80), null));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed when the weapon is missing");
        }

        private void AssertStandsOnItsFeet(EnemyAgent agent, Transform parent, Vector3 feet)
        {
            Assert.That(agent.transform.parent, Is.SameAs(parent));
            Assert.That(agent.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(agent.PatrolRoute, Is.Empty);
            var navAgent = agent.GetComponent<NavMeshAgent>();
            var ground = agent.transform.position - (Vector3.up * navAgent.baseOffset);
            Assert.That(Vector3.Distance(ground, feet), Is.LessThan(1e-4f));
            Assert.That(Vector3.Angle(agent.transform.forward, Vector3.left), Is.LessThan(1e-3f));
            Assert.That(navAgent.radius, Is.EqualTo(0.5f));
            Assert.That(agent.GetComponent<Damageable>().MaxHealth, Is.EqualTo(80));
        }

        [Test]
        public void FacingTowardsIgnoresHeightAndDefaultsToForward()
        {
            var rotation = SceneBuildHelpers.FacingTowards(Vector3.zero, new Vector3(0f, 5f, 3f));
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.forward), Is.LessThan(1e-3f));
            Assert.That(SceneBuildHelpers.FacingTowards(Vector3.one, Vector3.one), Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void EnemyBodyRejectsNonPositiveValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBody(0f, 0.5f, 3.5f, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBody(2f, 0.5f, 3.5f, 0));
        }

        [Test]
        public void RequireEmptyRejectsAScenePopulatedAlready()
        {
            SceneBuildHelpers.RequireEmpty(_scene, "test");
            SceneManager.MoveGameObjectToScene(new GameObject("Stray"), _scene);

            Assert.Throws<InvalidOperationException>(() => SceneBuildHelpers.RequireEmpty(_scene, "test"));
        }
    }
}
