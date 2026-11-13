using System;
using System.Linq;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    public sealed class BuildSceneListTests
    {
        private const string Other = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void BuildersSaveToTheListedPaths()
        {
            Assert.That(BootstrapSceneBuilder.DefaultScenePath, Is.EqualTo(BuildSceneList.BootstrapScene));
            Assert.That(EnemyTestSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.EnemyAiTestScene));
            Assert.That(MissionTestSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.MissionTestScene));
            Assert.That(DestructionTestSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.DestructionTestScene));
            Assert.That(CampaignSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.CampaignScene));
        }

        [Test]
        public void ListStartsWithTheBootstrapAndHasNoDuplicates()
        {
            Assert.That(BuildSceneList.Scenes[0], Is.EqualTo(BuildSceneList.BootstrapScene));
            Assert.That(BuildSceneList.Scenes, Is.Unique);
            foreach (var path in BuildSceneList.Scenes)
            {
                Assert.That(BootstrapSceneBuilder.ValidateScenePath(path), Is.EqualTo(path));
            }
        }

        [Test]
        public void ServerScenesAreOnlyTheNetScene()
        {
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { BuildSceneList.NetTestScene }));
        }

        [Test]
        public void ArrangePutsExistingListedScenesFirstInOrderAndKeepsTheRest()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Other, false),
                new EditorBuildSettingsScene(BuildSceneList.CampaignScene, false),
                new EditorBuildSettingsScene(BuildSceneList.BootstrapScene, true),
                new EditorBuildSettingsScene(Other, true),
            };

            var scenes = BuildSceneList.Arrange(current, path => path != BuildSceneList.MissionTestScene);

            var expected = BuildSceneList.Scenes.Where(path => path != BuildSceneList.MissionTestScene).Append(Other).ToArray();
            Assert.That(scenes.Select(s => s.path), Is.EqualTo(expected));
            Assert.That(scenes.Take(expected.Length - 1).All(s => s.enabled), Is.True, "listed scenes are enabled");
            Assert.That(scenes.Last().enabled, Is.False, "other scenes keep their first entry and its enabled flag");
        }

        [Test]
        public void ArrangeHandlesEmptyBuildSettingsAndRejectsNullCheck()
        {
            var scenes = BuildSceneList.Arrange(null, _ => false);

            Assert.That(scenes, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => BuildSceneList.Arrange(null, null));
        }

        [Test]
        public void WithStartScenePutsThePathFirst()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Other, false),
                new EditorBuildSettingsScene(BuildSceneList.BootstrapScene, false),
            };

            var scenes = BuildSceneList.WithStartScene(current, BuildSceneList.BootstrapScene);

            Assert.That(scenes.Select(s => s.path), Is.EqualTo(new[] { BuildSceneList.BootstrapScene, Other }));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[1].enabled, Is.False);
        }

        [Test]
        public void WithSceneEnablesInPlaceOrAppends()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(BuildSceneList.BootstrapScene, true),
                new EditorBuildSettingsScene(BuildSceneList.CampaignScene, false),
                new EditorBuildSettingsScene(Other, false),
                new EditorBuildSettingsScene(BuildSceneList.CampaignScene, true),
            };

            var enabled = BuildSceneList.WithScene(current, BuildSceneList.CampaignScene);
            Assert.That(
                enabled.Select(s => s.path),
                Is.EqualTo(new[] { BuildSceneList.BootstrapScene, BuildSceneList.CampaignScene, Other }));
            Assert.That(enabled[1].enabled, Is.True);

            var appended = BuildSceneList.WithScene(current, BuildSceneList.NetTestScene);
            Assert.That(appended.Last().path, Is.EqualTo(BuildSceneList.NetTestScene));
            Assert.That(appended.Last().enabled, Is.True);
        }

        [Test]
        public void RejectsPathsOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => BuildSceneList.WithScene(null, "Scenes/A.unity"));
            Assert.Throws<ArgumentException>(() => BuildSceneList.WithStartScene(null, "Assets/A.prefab"));
        }
    }
}
