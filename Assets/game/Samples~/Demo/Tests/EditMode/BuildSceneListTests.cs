using System;
using System.Linq;
using Shooter.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
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
            Assert.That(Campaign02Mission.ScenePath, Is.EqualTo(BuildSceneList.Campaign02Scene));
            Assert.That(MenuSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.MainMenuScene));
        }

        [Test]
        public void TheMenuLoadsTheListedScenes()
        {
            var root = Shooter.Runtime.MenuSceneSettings.LoadSceneRoot();
            var scenes = Shooter.Runtime.MenuSceneSettings.LoadScenes();

            Assert.That(Shooter.Core.MenuRoute.MainMenuSceneUnder(root, scenes), Is.EqualTo(BuildSceneList.MainMenuScene));
            Assert.That(Shooter.Core.MenuRoute.RangeSceneUnder(root, scenes), Is.EqualTo(BuildSceneList.BootstrapScene));
            Assert.That(Shooter.Core.MenuRoute.CampaignSceneUnder(root, scenes), Is.EqualTo(BuildSceneList.CampaignScene));
        }

        [Test]
        public void ListStartsWithTheMainMenuAndHasNoDuplicates()
        {
            Assert.That(BuildSceneList.Scenes[0], Is.EqualTo(BuildSceneList.MainMenuScene));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.BootstrapScene));
            Assert.That(BuildSceneList.Scenes, Is.Unique);
            foreach (var path in BuildSceneList.Scenes)
            {
                Assert.That(BootstrapSceneBuilder.ValidateScenePath(path), Is.EqualTo(path));
            }
        }

        [Test]
        public void ListHasTheTpsRangeAndDominationScenes()
        {
            Assert.That(BuildSceneList.TpsRangeScene, Is.EqualTo(DemoSample.Root + "/Scenes/TpsRange.unity"));
            Assert.That(BuildSceneList.DominationScene, Is.EqualTo(DemoSample.Root + "/Scenes/Domination.unity"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.TpsRangeScene));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.DominationScene));
        }

        [Test]
        public void ListHasTheFeatureScenesRightAfterTheTpsRange()
        {
            var scenes = BuildSceneList.Scenes.ToList();
            var expected = new[]
            {
                DemoSample.Root + "/Scenes/Playground.unity",
                DemoSample.Root + "/Scenes/HighDrop.unity",
                DemoSample.Root + "/Scenes/Downed.unity",
            };

            Assert.That(BuildSceneList.PlaygroundScene, Is.EqualTo(expected[0]));
            Assert.That(BuildSceneList.HighDropScene, Is.EqualTo(expected[1]));
            Assert.That(BuildSceneList.DownedScene, Is.EqualTo(expected[2]));
            Assert.That(BuildSceneList.FeatureScenes, Is.EqualTo(expected));
            var start = scenes.IndexOf(BuildSceneList.TpsRangeScene) + 1;
            Assert.That(scenes.Skip(start).Take(expected.Length), Is.EqualTo(expected));
            Assert.That(scenes.Where(path => path.Contains("/Feature_")), Is.Empty, "the per-feature scenes are gone");
        }

        [Test]
        public void WithoutDropsTheGivenScenesAndKeepsTheRestInOrder()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene("Assets/A.unity", true),
                new EditorBuildSettingsScene("Assets/Feature_Jump.unity", true),
                new EditorBuildSettingsScene("Assets/B.unity", false),
            };

            var kept = BuildSceneList.Without(current, new[] { "Assets/Feature_Jump.unity", "Assets/Missing.unity" });

            Assert.That(kept.Select(s => s.path), Is.EqualTo(new[] { "Assets/A.unity", "Assets/B.unity" }));
            Assert.That(kept[1].enabled, Is.False);
            Assert.Throws<System.ArgumentNullException>(() => BuildSceneList.Without(current, null));
        }

        [Test]
        public void ListHasEveryModeSceneNamedAfterItsRules()
        {
            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                var scene = BuildSceneList.ModeScene(name);
                Assert.That(scene, Is.EqualTo(DemoSample.Root + "/Scenes/" + name + ".unity"), name);
                Assert.That(BuildSceneList.Scenes, Has.Member(scene), name);
            }

            Assert.That(BuildSceneList.FreeForAllScene, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.FreeForAllName)));
            Assert.That(BuildSceneList.SearchAndDestroyScene, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.SearchAndDestroyName)));
            Assert.That(BuildSceneList.CaptureTheFlagScene, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.CaptureTheFlagName)));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.CaptureTheFlagScene));
            Assert.That(BuildSceneList.BattleRoyaleScene, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.BattleRoyaleName)));
            Assert.Throws<ArgumentException>(() => BuildSceneList.ModeScene("TeamDeathmatch"));
            Assert.That(BuildSceneList.Scenes.Last(), Is.EqualTo(BuildSceneList.NetTestScene), "the mode scenes go before the net scene");
        }

        [Test]
        public void ListHasTheInfectedAndExtractionScenesRightAfterTheBattleRoyale()
        {
            var scenes = BuildSceneList.Scenes.ToList();

            Assert.That(BuildSceneList.InfectedScene, Is.EqualTo(DemoSample.Root + "/Scenes/Infected.unity"));
            Assert.That(BuildSceneList.ExtractionScene, Is.EqualTo(DemoSample.Root + "/Scenes/Extraction.unity"));
            Assert.That(BuildSceneList.ModeScene(GameRulesAssetBuilder.InfectedName), Is.EqualTo(BuildSceneList.InfectedScene));
            Assert.That(BuildSceneList.ModeScene(GameRulesAssetBuilder.ExtractionName), Is.EqualTo(BuildSceneList.ExtractionScene));
            var start = scenes.IndexOf(BuildSceneList.BattleRoyaleScene) + 1;
            Assert.That(
                scenes.Skip(start).Take(3),
                Is.EqualTo(new[] { BuildSceneList.InfectedScene, BuildSceneList.ExtractionScene, BuildSceneList.ModeTestScene }));
            Assert.That(BuildSceneList.WebScenes, Has.No.Member(BuildSceneList.InfectedScene), "the menu has no button for the modes");
        }

        [Test]
        public void ArrangeSkipsTheModeScenesUntilTheyAreBuilt()
        {
            var modeScenes = GameRulesAssetBuilder.ModeNames.Select(BuildSceneList.ModeScene).ToArray();

            var scenes = BuildSceneList.Arrange(null, path => !modeScenes.Contains(path));

            Assert.That(scenes.Select(s => s.path), Is.EqualTo(BuildSceneList.Scenes.Except(modeScenes)));
        }

        [Test]
        public void ArrangeSkipsTheNewScenesUntilTheyAreBuilt()
        {
            var scenes = BuildSceneList.Arrange(
                null, path => path != BuildSceneList.TpsRangeScene && path != BuildSceneList.DominationScene);

            Assert.That(scenes.Select(s => s.path), Has.No.Member(BuildSceneList.TpsRangeScene));
            Assert.That(scenes.Select(s => s.path), Has.No.Member(BuildSceneList.DominationScene));
            Assert.That(scenes[0].path, Is.EqualTo(BuildSceneList.MainMenuScene));
        }

        [Test]
        public void ServerScenesStartWithTheNetSceneAndHaveTheMaps()
        {
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { BuildSceneList.NetTestScene, BuildSceneList.NetArena01Scene }));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.NetArena01Scene), "clients load the server's map too");
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
