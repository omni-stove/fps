using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class MissionTestSceneBuilderTests
    {
        private const string Bootstrap = "Assets/game/Scenes/Bootstrap.unity";

        private MissionDefinition _definition;
        private WeaponDefinition[] _weapons;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = "TestMission";
            MissionTestSceneBuilder.Configure(_definition);
            _weapons = new[] { ScriptableObject.CreateInstance<WeaponDefinition>(), ScriptableObject.CreateInstance<WeaponDefinition>() };
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(_scene, true);
            Object.DestroyImmediate(_definition);
            foreach (var weapon in _weapons)
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void TestMissionIsReachThenKillThreeThenExtract()
        {
            var mission = _definition.CreateMission();

            Assert.That(mission.Objectives.Count, Is.EqualTo(3));
            Assert.That(mission.Objectives[0].Id, Is.EqualTo(MissionTestSceneBuilder.ReachAreaId));
            Assert.That(mission.Objectives[1].Id, Is.EqualTo(MissionTestSceneBuilder.KillTargetsId));
            Assert.That(mission.Objectives[1].RequiredCount, Is.EqualTo(MissionTestSceneBuilder.TargetCount));
            Assert.That(mission.Objectives[1].Prerequisites, Is.EqualTo(new[] { MissionTestSceneBuilder.ReachAreaId }));
            Assert.That(mission.Objectives[2].Id, Is.EqualTo(MissionTestSceneBuilder.ExtractId));
            Assert.That(mission.Objectives[2].Prerequisites, Is.EqualTo(new[] { MissionTestSceneBuilder.KillTargetsId }));
        }

        [Test]
        public void EveryMissionTextTheBuildersWriteIsInEveryLanguage()
        {
            var keys = new List<string> { MissionTestSceneBuilder.MissionNameKey };
            keys.AddRange(MissionTestSceneBuilder.TestObjectives().Select(o => o.DescriptionKey));
            foreach (var spec in CampaignSceneBuilder.Missions())
            {
                keys.Add(spec.NameKey);
                keys.AddRange(spec.Objectives.Select(o => o.DescriptionKey));
            }

            foreach (var language in GameLanguages.All)
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, $"{GameLanguages.Code(language)}: {key}");
                }
            }
        }

        [Test]
        public void PopulatedSceneIsWiredInCode()
        {
            var layout = MissionTestSceneBuilder.PopulateScene(_scene, _definition, _weapons);

            Assert.That(_scene.GetRootGameObjects(), Has.Length.EqualTo(5));
            Assert.That(layout.Bootstrap.GetComponent<GameBootstrap>(), Is.Not.Null);
            Assert.That(layout.Bootstrap.GetComponent<GameBootstrap>().WeaponDefinitions, Is.EqualTo(_weapons));
            Assert.That(layout.Runner.Definition, Is.SameAs(_definition));

            Assert.That(layout.ReachArea.ObjectiveId, Is.EqualTo(MissionTestSceneBuilder.ReachAreaId));
            Assert.That(layout.ReachArea.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.ReachArea.GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(layout.ReachArea.GetComponent<Rigidbody>().isKinematic, Is.True);

            Assert.That(layout.KillTargets.ObjectiveId, Is.EqualTo(MissionTestSceneBuilder.KillTargetsId));
            Assert.That(layout.KillTargets.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Targets.Count, Is.EqualTo(MissionTestSceneBuilder.TargetCount));
            foreach (var target in layout.Targets)
            {
                Assert.That(target.transform.parent, Is.SameAs(layout.KillTargets.transform));
                Assert.That(target.GetComponent<Collider>().isTrigger, Is.False, "targets must be solid to be shot");
            }

            Assert.That(layout.Extraction.ObjectiveId, Is.EqualTo(MissionTestSceneBuilder.ExtractId));
            Assert.That(layout.Extraction.Runner, Is.SameAs(layout.Runner));
        }

        [Test]
        public void AreaTriggerStandsOnTheFloorAndIsTallerThanThePlayer()
        {
            var layout = MissionTestSceneBuilder.PopulateScene(_scene, _definition, _weapons);
            var collider = layout.ReachArea.GetComponent<BoxCollider>();
            var transform = collider.transform;

            // Edit Mode では物理の同期を待たずに確かめたいので、Collider.bounds ではなく Transform から計算する。
            var bottom = transform.TransformPoint(collider.center - new Vector3(0f, collider.size.y / 2f, 0f)).y;
            var top = transform.TransformPoint(collider.center + new Vector3(0f, collider.size.y / 2f, 0f)).y;

            Assert.That(bottom, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(top, Is.EqualTo(MissionTestSceneBuilder.AreaTriggerHeight).Within(1e-3f));
            Assert.That(top - bottom, Is.GreaterThan(GameBootstrap.PlayerHeight));
        }

        [Test]
        public void GeneratedLayoutCanBeCompletedInOrder()
        {
            var layout = MissionTestSceneBuilder.PopulateScene(_scene, _definition, _weapons);
            var player = new GameObject("Player");
            try
            {
                player.tag = GameBootstrap.PlayerTag;
                var collider = player.AddComponent<CapsuleCollider>();

                Assert.That(layout.Runner.TryStart(), Is.True);
                Assert.That(layout.ReachArea.Bind(layout.Runner), Is.True);
                Assert.That(layout.KillTargets.Bind(layout.Runner), Is.True);
                Assert.That(layout.Extraction.Bind(layout.Runner), Is.True);
                var mission = layout.Runner.Mission;

                layout.ReachArea.HandleEnter(collider);
                Assert.That(mission.GetState(MissionTestSceneBuilder.ReachAreaId), Is.EqualTo(ObjectiveState.Completed));

                layout.Extraction.HandleEnter(collider);
                layout.Extraction.HandleExit(collider);
                Assert.That(mission.GetState(MissionTestSceneBuilder.ExtractId), Is.EqualTo(ObjectiveState.Locked));

                foreach (var target in layout.Targets)
                {
                    target.Kill();
                }

                Assert.That(mission.GetState(MissionTestSceneBuilder.KillTargetsId), Is.EqualTo(ObjectiveState.Completed));

                LogAssert.Expect(LogType.Log, MissionReporter.CompletedLine);
                layout.Extraction.HandleEnter(collider);
                Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RefusesToPopulateANonEmptyScene()
        {
            SceneManager.MoveGameObjectToScene(new GameObject("Stray"), _scene);

            Assert.Throws<InvalidOperationException>(() => MissionTestSceneBuilder.PopulateScene(_scene, _definition, _weapons));
            Assert.That(_scene.rootCount, Is.EqualTo(1), "nothing is left behind");
        }

        [Test]
        public void RefusesToPopulateWithoutDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => MissionTestSceneBuilder.PopulateScene(_scene, null, _weapons));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void RefusesToPopulateWithoutAWeapon()
        {
            Assert.Throws<ArgumentNullException>(() => MissionTestSceneBuilder.PopulateScene(_scene, _definition, null));
            Assert.Throws<ArgumentException>(() => MissionTestSceneBuilder.PopulateScene(_scene, _definition, Array.Empty<WeaponDefinition>()));
            Assert.Throws<ArgumentException>(() => MissionTestSceneBuilder.PopulateScene(_scene, _definition, new WeaponDefinition[] { null }));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void AppendsSceneAndKeepsTheFirstOne()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Bootstrap, true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false),
            };

            var scenes = MissionTestSceneBuilder.WithSceneAppended(current, MissionTestSceneBuilder.ScenePath);

            Assert.That(scenes, Has.Length.EqualTo(3));
            Assert.That(scenes[0].path, Is.EqualTo(Bootstrap));
            Assert.That(scenes[1].enabled, Is.False, "other scenes keep their enabled flag");
            Assert.That(scenes[2].path, Is.EqualTo(MissionTestSceneBuilder.ScenePath));
            Assert.That(scenes[2].enabled, Is.True);
        }

        [Test]
        public void EnablesAnExistingEntryInPlace()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Bootstrap, true),
                new EditorBuildSettingsScene(MissionTestSceneBuilder.ScenePath, false),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
                new EditorBuildSettingsScene(MissionTestSceneBuilder.ScenePath, true),
            };

            var scenes = MissionTestSceneBuilder.WithSceneAppended(current, MissionTestSceneBuilder.ScenePath);

            Assert.That(scenes, Has.Length.EqualTo(3), "duplicates are dropped");
            Assert.That(scenes[1].path, Is.EqualTo(MissionTestSceneBuilder.ScenePath));
            Assert.That(scenes[1].enabled, Is.True);
            Assert.That(MissionTestSceneBuilder.WithSceneAppended(null, MissionTestSceneBuilder.ScenePath), Has.Length.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("TestMission.asset")]
        [TestCase("Assets/TestMission.unity")]
        [TestCase("Assets/../TestMission.asset")]
        public void RejectsAssetPathOutsideAssets(string path)
        {
            Assert.Throws<ArgumentException>(() => MissionTestSceneBuilder.ValidateAssetPath(path));
        }

        [Test]
        public void CommittedAssetsMatchTheBuilder()
        {
            var definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionTestSceneBuilder.DefinitionPath);
            Assume.That(definition, Is.Not.Null, $"run {nameof(MissionTestSceneBuilder)}.Build first");
            Assume.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MissionTestSceneBuilder.ScenePath),
                Is.Not.Null,
                $"run {nameof(MissionTestSceneBuilder)}.Build first");

            Assert.That(definition.CreateMission().Objectives.Count, Is.EqualTo(3));
            Assert.That(definition.DisplayNameKey, Is.EqualTo(MissionTestSceneBuilder.MissionNameKey));
            Assert.That(
                definition.Objectives.Select(o => o.DescriptionKey),
                Is.EqualTo(MissionTestSceneBuilder.TestObjectives().Select(o => o.DescriptionKey)));

            var scene = EditorSceneManager.OpenScene(MissionTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                MissionRunner runner = null;
                GameBootstrap bootstrap = null;
                var triggers = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    runner = runner != null ? runner : root.GetComponent<MissionRunner>();
                    bootstrap = bootstrap != null ? bootstrap : root.GetComponent<GameBootstrap>();
                    triggers += root.GetComponents<ReachAreaTrigger>().Length;
                }

                Assert.That(runner, Is.Not.Null);
                Assert.That(runner.Definition, Is.SameAs(definition));
                Assert.That(triggers, Is.EqualTo(2));

                var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.RiflePath);
                Assert.That(rifle, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build");
                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(TestScenes.ExpectedPlayerWeapons()), $"rerun {nameof(MissionTestSceneBuilder)}.Build");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
