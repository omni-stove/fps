using System;
using System.Collections.Generic;
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
    /// <summary>
    /// <see cref="DominationSceneBuilder"/> の配置と、地形の後に置く物 (<see cref="DominationSceneBuilder.PopulateScene"/>) を確かめる。
    /// NavMesh を焼いて保存する処理は、開いているシーンを置き換える (NewScene Single) ので
    /// <c>-executeMethod Fps.Editor.DominationSceneBuilder.Build</c> と <see cref="DominationSmokeRun"/> で確かめる。
    /// </summary>
    public sealed class DominationSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

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
        public void SavesToTheDominationSceneOfTheBuildList()
        {
            Assert.That(DominationSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.DominationScene));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.DominationScene));
            Assert.That(DominationSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Domination/NavMesh-Floor.asset"));
            Assert.That(DominationSceneBuilder.MenuPath, Is.EqualTo("Fps/Build Domination Scene"));
        }

        [Test]
        public void ThreeControlPointsLieApartOnTheFloor()
        {
            var points = DominationSceneBuilder.ControlPoints;
            Assert.That(points, Has.Count.EqualTo(3));

            var radius = DominationSceneBuilder.ControlPointRadius;
            for (var i = 0; i < points.Count; i++)
            {
                Assert.That(points[i].y, Is.EqualTo(0f), "the zone sits on the floor so its marker lies flat on it");
                AssertInsideBoundary(points[i], radius, $"point {i}");
                for (var j = i + 1; j < points.Count; j++)
                {
                    Assert.That(Flat(points[i] - points[j]).magnitude, Is.GreaterThan(radius * 2f), $"points {i} and {j} overlap");
                }
            }

            Assert.That(DominationSceneBuilder.ControlPointName(0), Is.EqualTo("ControlPoint-A"));
            Assert.That(DominationSceneBuilder.ControlPointName(2), Is.EqualTo("ControlPoint-C"));
        }

        [Test]
        public void ThePlayerStartsWhereGameBootstrapPutsItAndTheSpawnsAreClear()
        {
            var alpha = DominationSceneBuilder.AlphaSpawns;
            Assert.That(alpha[0].Feet, Is.EqualTo(ModeSceneBuilder.PlayerFeet));
            Assert.That(alpha[0].Facing, Is.EqualTo(Vector3.forward));
            Assert.That(DominationSceneBuilder.BotObjectives.Count, Is.LessThanOrEqualTo(DominationSceneBuilder.BravoSpawns.Count));

            var spawns = new List<ModeSpawnPlacement>(alpha);
            spawns.AddRange(DominationSceneBuilder.BravoSpawns);
            foreach (var spawn in spawns)
            {
                AssertInsideBoundary(spawn.Feet, EnemyTestSceneBuilder.EnemyRadius, $"spawn {spawn.Feet}");
                AssertClearOfObstacles(spawn.Feet, EnemyTestSceneBuilder.EnemyRadius * 4f, $"spawn {spawn.Feet}");
                foreach (var point in DominationSceneBuilder.ControlPoints)
                {
                    Assert.That(
                        Flat(spawn.Feet - point).magnitude,
                        Is.GreaterThan(DominationSceneBuilder.ControlPointRadius),
                        $"spawn {spawn.Feet} is inside the control point at {point}");
                }
            }
        }

        [Test]
        public void TheObstaclesHaveUniqueNamesAndStayOutOfTheLoiterPaths()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obstacle in DominationSceneBuilder.Obstacles)
            {
                Assert.That(names.Add(obstacle.Name), Is.True, $"'{obstacle.Name}' is used twice");
                Assert.That(obstacle.Center.y - (obstacle.Size.y / 2f), Is.EqualTo(0f).Within(Epsilon), $"{obstacle.Name} stands on the floor");
            }

            for (var i = 0; i < DominationSceneBuilder.BotObjectives.Count; i++)
            {
                foreach (var point in DominationSceneBuilder.BotPatrolRoute(i))
                {
                    AssertClearOfObstacles(point, EnemyTestSceneBuilder.EnemyRadius * 2f, $"bot #{i + 1} patrol point {point}");
                }
            }
        }

        [Test]
        public void EachBotLoitersInsideEveryPointLongEnoughToCaptureIt()
        {
            var radius = DominationSceneBuilder.ControlPointRadius;
            var loiter = DominationSceneBuilder.LoiterOffsets;
            for (var i = 0; i < DominationSceneBuilder.BotObjectives.Count; i++)
            {
                var objectives = DominationSceneBuilder.BotObjectives[i];
                Assert.That(objectives, Is.EquivalentTo(new[] { 0, 1, 2 }), $"bot #{i + 1} visits every point once per round");

                var route = DominationSceneBuilder.BotPatrolRoute(i);
                var perPoint = loiter.Count * DominationSceneBuilder.LoiterLaps;
                Assert.That(route, Has.Count.EqualTo(objectives.Count * perPoint));

                for (var k = 0; k < route.Count; k++)
                {
                    var center = DominationSceneBuilder.ControlPoints[objectives[k / perPoint]];
                    Assert.That(Flat(route[k] - center).magnitude, Is.LessThan(radius - EnemyTestSceneBuilder.EnemyRadius), $"bot #{i + 1} point {k}");
                }
            }

            // 拠点の中を歩き回る時間が、1 人で占領し終える時間を超えること。
            var lap = 0f;
            for (var k = 0; k < loiter.Count; k++)
            {
                lap += Flat(loiter[(k + 1) % loiter.Count] - loiter[k]).magnitude;
            }

            var seconds = lap * DominationSceneBuilder.LoiterLaps / EnemyTestSceneBuilder.EnemySpeed;
            Assert.That(seconds, Is.GreaterThan(DominationRunner.DefaultCaptureSeconds));
        }

        [Test]
        public void BotPatrolRouteRejectsAnUnknownBot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DominationSceneBuilder.BotPatrolRoute(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DominationSceneBuilder.BotPatrolRoute(DominationSceneBuilder.BotObjectives.Count));
        }

        [Test]
        public void PopulatesABootstrapInFirstPersonWithTheRulesAndARunnerWithBothTeamsSpawns()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.DominationRules());

            var runner = DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, rifle);

            var bootstrap = Find(DominationSceneBuilder.BootstrapObjectName).GetComponent<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(bootstrap.CreatesFloor, Is.False, "the scene has its own floor with a baked NavMesh");
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(new[] { rifle }));
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(rules));

            Assert.That(runner.name, Is.EqualTo(DominationSceneBuilder.RunnerObjectName));
            Assert.That(runner.gameObject.scene, Is.EqualTo(_scene));
            AssertSpawns(runner.SpawnPoints(Team.Alpha), DominationSceneBuilder.AlphaSpawns);
            AssertSpawns(runner.SpawnPoints(Team.Bravo), DominationSceneBuilder.BravoSpawns);
        }

        [Test]
        public void PopulatesTheControlPointsWithMarkersThatDoNotBlock()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, NewRules(GameRulesAssetBuilder.DominationRules()), rifle);

            var root = Find(DominationSceneBuilder.ControlPointsRootName).transform;
            Assert.That(root.childCount, Is.EqualTo(DominationSceneBuilder.ControlPoints.Count));
            for (var i = 0; i < DominationSceneBuilder.ControlPoints.Count; i++)
            {
                var point = root.Find(DominationSceneBuilder.ControlPointName(i));
                Assert.That(point, Is.Not.Null, DominationSceneBuilder.ControlPointName(i));
                Assert.That(point.position, Is.EqualTo(DominationSceneBuilder.ControlPoints[i]));

                var zone = point.GetComponent<ControlPointZone>();
                Assert.That(zone, Is.Not.Null);
                Assert.That(zone.Index, Is.EqualTo(i));
                Assert.That(zone.Radius, Is.EqualTo(DominationSceneBuilder.ControlPointRadius));
                Assert.That(zone.Height, Is.EqualTo(DominationSceneBuilder.ControlPointHeight));

                foreach (var name in new[] { DominationSceneBuilder.PoleName, DominationSceneBuilder.FlagName })
                {
                    var marker = point.Find(name);
                    Assert.That(marker, Is.Not.Null, $"{point.name}/{name}");
                    Assert.That(marker.GetComponent<Renderer>(), Is.Not.Null, $"{point.name}/{name} is visible");
                    Assert.That(marker.GetComponent<Collider>(), Is.Null, $"{point.name}/{name} must not block shots or movement");
                }
            }
        }

        [Test]
        public void PopulatesBravoBotsThatPatrolThePointsAndTargetThePlayer()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, NewRules(GameRulesAssetBuilder.DominationRules()), rifle);

            var root = Find(DominationSceneBuilder.BotsRootName).transform;
            Assert.That(root.childCount, Is.EqualTo(DominationSceneBuilder.BotObjectives.Count));
            for (var i = 0; i < DominationSceneBuilder.BotObjectives.Count; i++)
            {
                var bot = root.Find(DominationSceneBuilder.BotName(i + 1));
                Assert.That(bot, Is.Not.Null, DominationSceneBuilder.BotName(i + 1));

                var agent = bot.GetComponent<EnemyAgent>();
                Assert.That(agent, Is.Not.Null);
                Assert.That(agent.Id, Is.EqualTo(DominationSceneBuilder.BotId(i + 1)));
                Assert.That(agent.WeaponDefinition, Is.SameAs(rifle));
                Assert.That(agent.PatrolRoute, Is.EqualTo(DominationSceneBuilder.BotPatrolRoute(i)));
                Assert.That(bot.GetComponent<Damageable>(), Is.Not.Null);
                Assert.That(bot.GetComponent<SideMember>(), Is.Null, "with a SideMember the bot would look for a side the player does not have");

                var feet = agent.GroundPosition;
                Assert.That(Flat(feet - DominationSceneBuilder.BravoSpawns[i].Feet).magnitude, Is.LessThan(Epsilon));
            }
        }

        [Test]
        public void RefusesMissingOrInvalidArgumentsAndPlacesNothing()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.DominationRules());
            var broken = NewRules(GameRulesAssetBuilder.DominationRules());
            broken.ReserveAmmoMultiplier = -1f;

            Assert.Throws<ArgumentNullException>(() => DominationSceneBuilder.PopulateScene(_scene, null, rules, rifle));
            Assert.Throws<ArgumentException>(() => DominationSceneBuilder.PopulateScene(_scene, Array.Empty<WeaponDefinition>(), rules, rifle));
            Assert.Throws<ArgumentException>(() => DominationSceneBuilder.PopulateScene(_scene, new WeaponDefinition[] { null }, rules, rifle));
            Assert.Throws<ArgumentNullException>(() => DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, null, rifle));
            Assert.Throws<ArgumentNullException>(() => DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, null));
            Assert.Throws<GameRulesDefinitionException>(() => DominationSceneBuilder.PopulateScene(_scene, new[] { rifle }, broken, rifle));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
        }

        [Test]
        public void CreateLevelRefusesANonEmptyScene()
        {
            var stray = new GameObject("Stray");
            SceneManager.MoveGameObjectToScene(stray, _scene);

            Assert.Throws<InvalidOperationException>(() => DominationSceneBuilder.CreateLevel(_scene));
            Assert.That(_scene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void TheSmokeRunReadsTheTimeLimitArgument()
        {
            Assert.That(DominationSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(DominationSmokeRun.DefaultSeconds));
            Assert.That(DominationSmokeRun.ParseSeconds(new[] { "-batchmode", DominationSmokeRun.SecondsArgument, "25.5" }), Is.EqualTo(25.5f));
            Assert.Throws<ArgumentNullException>(() => DominationSmokeRun.ParseSeconds(null));
            Assert.Throws<ArgumentException>(() => DominationSmokeRun.ParseSeconds(new[] { DominationSmokeRun.SecondsArgument }));
            Assert.Throws<ArgumentException>(() => DominationSmokeRun.ParseSeconds(new[] { DominationSmokeRun.SecondsArgument, "abc" }));
            Assert.Throws<ArgumentException>(
                () => DominationSmokeRun.ParseSeconds(new[] { DominationSmokeRun.SecondsArgument, "5" }), "shorter than a capture");
            Assert.That(DominationSmokeRun.DefaultSeconds, Is.GreaterThan(DominationRunner.DefaultCaptureSeconds));
        }

        [Test]
        public void CommittedSceneWiresTheDominationRulesAndThreePoints()
        {
            var path = DominationSceneBuilder.ScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(DominationSceneBuilder)}.Build first");

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            var bootstrap = Find(DominationSceneBuilder.BootstrapObjectName).GetComponent<GameBootstrap>();
            Assert.That(bootstrap.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(TestScenes.ExpectedPlayerWeapons()));
            Assert.That(
                bootstrap.RulesDefinition,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.DominationPath)));

            Assert.That(Find(DominationSceneBuilder.RunnerObjectName).GetComponent<DominationRunner>(), Is.Not.Null);
            Assert.That(
                Find(DominationSceneBuilder.ControlPointsRootName).GetComponentsInChildren<ControlPointZone>(),
                Has.Length.EqualTo(DominationSceneBuilder.ControlPoints.Count));
            Assert.That(
                Find(DominationSceneBuilder.BotsRootName).GetComponentsInChildren<EnemyAgent>(),
                Has.Length.EqualTo(DominationSceneBuilder.BotObjectives.Count));

            var surface = Find(DominationSceneBuilder.FloorName).GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.navMeshData, Is.Not.Null, "the NavMesh is baked and saved next to the scene");
        }

        private static void AssertSpawns(IReadOnlyList<Transform> actual, IReadOnlyList<ModeSpawnPlacement> expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i], Is.Not.Null);
                Assert.That(actual[i].position, Is.EqualTo(expected[i].Feet));
                Assert.That(Vector3.Dot(actual[i].forward, expected[i].Facing), Is.GreaterThan(0.999f));
            }
        }

        private static void AssertInsideBoundary(Vector3 position, float margin, string what)
        {
            var local = position - DominationSceneBuilder.FloorCenter;
            var inner = ModeSceneBuilder.BoundaryWallThickness + margin;
            Assert.That(Mathf.Abs(local.x), Is.LessThan((DominationSceneBuilder.FloorScale.x * 5f) - inner), $"{what} is inside the boundary walls");
            Assert.That(Mathf.Abs(local.z), Is.LessThan((DominationSceneBuilder.FloorScale.z * 5f) - inner), $"{what} is inside the boundary walls");
        }

        private static void AssertClearOfObstacles(Vector3 feet, float margin, string what)
        {
            foreach (var obstacle in DominationSceneBuilder.Obstacles)
            {
                var bounds = new Bounds(obstacle.Center, obstacle.Size);
                bounds.Expand(margin);
                Assert.That(bounds.Contains(feet), Is.False, $"{what} is clear of {obstacle.Name}");
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, spec);
            return weapon;
        }

        private GameRulesDefinition NewRules(GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
        }

        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail($"'{name}' is not in the scene");
            return null;
        }
    }
}
