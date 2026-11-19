using System;
using System.Linq;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 既存のビルダー (EnemyTestSceneBuilder・MissionTestSceneBuilder) の出力が切り出しの前後で変わらないことは
    // それぞれのテストで確かめているので、ここでは共通部分そのものだけを確かめる。
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
        public void PerimeterWallsCloseTheAreaJustOutsideItsEdges()
        {
            var root = SceneBuildHelpers.CreatePerimeterWalls(_scene, null, new Vector3(10f, 2f, -5f), new Vector2(20f, 15f), 8f, 1f);

            Assert.That(root.name, Is.EqualTo(SceneBuildHelpers.PerimeterName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.childCount, Is.EqualTo(4));

            var north = root.transform.Find("Perimeter_North");
            var south = root.transform.Find("Perimeter_South");
            var east = root.transform.Find("Perimeter_East");
            var west = root.transform.Find("Perimeter_West");

            Assert.That(north.position, Is.EqualTo(new Vector3(10f, 6f, 10.5f)));
            Assert.That(north.localScale, Is.EqualTo(new Vector3(42f, 8f, 1f)));
            Assert.That(south.position, Is.EqualTo(new Vector3(10f, 6f, -20.5f)));
            Assert.That(east.position, Is.EqualTo(new Vector3(30.5f, 6f, -5f)));
            Assert.That(east.localScale, Is.EqualTo(new Vector3(1f, 8f, 30f)));
            Assert.That(west.position, Is.EqualTo(new Vector3(-10.5f, 6f, -5f)));

            foreach (Transform wall in root.transform)
            {
                Assert.That(wall.GetComponent<Collider>(), Is.Not.Null, wall.name);
                Assert.That(wall.gameObject.isStatic, Is.True, wall.name);
            }
        }

        [Test]
        public void PerimeterWallsRejectAnEmptyArea()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SceneBuildHelpers.CreatePerimeterWalls(_scene, null, Vector3.zero, new Vector2(0f, 5f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => SceneBuildHelpers.CreatePerimeterWalls(_scene, null, Vector3.zero, Vector2.one, 0f, 1f));
        }

        [Test]
        public void PlaneHalfExtentsAreFiveMetresPerScale()
        {
            Assert.That(SceneBuildHelpers.PlaneHalfExtents(new Vector3(8f, 1f, 6f)), Is.EqualTo(new Vector2(40f, 30f)));
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

            var shared = BuildSceneList.WithScene(current, CampaignSceneBuilder.ScenePath);
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

        [Test]
        public void EnemyGetsTheStealthFromTheDemoStep()
        {
            Assert.That(SceneBuildHelpers.EnemySteps, Has.Member((Action<GameObject>)EnemyStealthBuildStep.Apply));

            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var agent = SceneBuildHelpers.CreateEnemy(
                    _scene, null, "Enemy", "enemy", Vector3.zero, Quaternion.identity, Array.Empty<Vector3>(),
                    new EnemyBody(2f, 0.5f, 3.5f, 80), rifle);

                Assert.That(agent.GetComponents<EnemyStealth>(), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void EnemyStepsRunInTheOrderTheyWereRegisteredAndOnlyOnce()
        {
            var calls = new System.Collections.Generic.List<string>();
            Action<GameObject> first = enemy => calls.Add("first:" + enemy.name);
            Action<GameObject> second = enemy => calls.Add("second:" + enemy.name);
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            SceneBuildHelpers.RegisterEnemyStep(first);
            SceneBuildHelpers.RegisterEnemyStep(second);
            SceneBuildHelpers.RegisterEnemyStep(first);
            try
            {
                SceneBuildHelpers.CreateEnemy(
                    _scene, null, "Grunt", "grunt", Vector3.zero, Quaternion.identity, Array.Empty<Vector3>(),
                    new EnemyBody(2f, 0.5f, 3.5f, 80), rifle);

                Assert.That(calls, Is.EqualTo(new[] { "first:Grunt", "second:Grunt" }));
                Assert.That(SceneBuildHelpers.EnemySteps.Count(s => s == first), Is.EqualTo(1));
            }
            finally
            {
                SceneBuildHelpers.UnregisterEnemyStep(first);
                SceneBuildHelpers.UnregisterEnemyStep(second);
                Object.DestroyImmediate(rifle);
            }

            Assert.That(SceneBuildHelpers.EnemySteps, Has.No.Member(first).And.No.Member(second));
            Assert.Throws<ArgumentNullException>(() => SceneBuildHelpers.RegisterEnemyStep(null));
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

        public static GameObject AssertPerimeterEncloses(Scene scene, float minX, float maxX, float minZ, float maxZ, float baseY)
        {
            var found = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == SceneBuildHelpers.PerimeterName)
                .ToArray();
            Assert.That(found, Has.Length.EqualTo(1), $"the scene '{scene.name}' is walled in exactly once");
            var root = found[0];
            Assert.That(root.childCount, Is.EqualTo(4), "four walls");

            var north = PerimeterWall(root, "Perimeter_North");
            var south = PerimeterWall(root, "Perimeter_South");
            var east = PerimeterWall(root, "Perimeter_East");
            var west = PerimeterWall(root, "Perimeter_West");

            const float tolerance = 1e-3f;
            Assert.That(north.position.z - (north.lossyScale.z / 2f), Is.EqualTo(maxZ).Within(tolerance), "the north wall stands on the north edge");
            Assert.That(south.position.z + (south.lossyScale.z / 2f), Is.EqualTo(minZ).Within(tolerance), "the south wall stands on the south edge");
            Assert.That(east.position.x - (east.lossyScale.x / 2f), Is.EqualTo(maxX).Within(tolerance), "the east wall stands on the east edge");
            Assert.That(west.position.x + (west.lossyScale.x / 2f), Is.EqualTo(minX).Within(tolerance), "the west wall stands on the west edge");

            foreach (var wall in new[] { north, south })
            {
                Assert.That(wall.position.x - (wall.lossyScale.x / 2f), Is.LessThanOrEqualTo(minX + tolerance), wall.name + " reaches the west edge");
                Assert.That(wall.position.x + (wall.lossyScale.x / 2f), Is.GreaterThanOrEqualTo(maxX - tolerance), wall.name + " reaches the east edge");
            }

            foreach (var wall in new[] { east, west })
            {
                Assert.That(wall.position.z - (wall.lossyScale.z / 2f), Is.LessThanOrEqualTo(minZ + tolerance), wall.name + " reaches the south edge");
                Assert.That(wall.position.z + (wall.lossyScale.z / 2f), Is.GreaterThanOrEqualTo(maxZ - tolerance), wall.name + " reaches the north edge");
            }

            foreach (var wall in new[] { north, south, east, west })
            {
                Assert.That(wall.position.y - (wall.lossyScale.y / 2f), Is.EqualTo(baseY).Within(tolerance), wall.name + " stands on the floor");
                Assert.That(wall.lossyScale.y, Is.EqualTo(SceneBuildHelpers.DefaultPerimeterHeight).Within(tolerance), wall.name + " is too high to jump over");
                Assert.That(wall.GetComponent<Collider>(), Is.Not.Null, wall.name);
                Assert.That(wall.gameObject.isStatic, Is.True, wall.name);
            }

            return root.gameObject;
        }

        private static Transform PerimeterWall(Transform root, string name)
        {
            var wall = root.Find(name);
            Assert.That(wall, Is.Not.Null, name);
            return wall;
        }
    }
}
