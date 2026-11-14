using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MenuScenesTests
    {
        private const string _root =KitPaths.DefaultRoot;

        [Test]
        public void TheDefaultsAreTheMenuRouteNames()
        {
            var scenes = MenuScenes.Default;

            Assert.That(scenes.MainMenu, Is.EqualTo(MenuRoute.MainMenuSceneName));
            Assert.That(scenes.Range, Is.EqualTo(MenuRoute.RangeSceneName));
            Assert.That(scenes.Campaign, Is.EqualTo(MenuRoute.CampaignSceneName));
            Assert.That(scenes.Net, Is.EqualTo(MenuRoute.NetSceneName));
        }

        [Test]
        public void EachNameOverridesOnlyItsOwnScene()
        {
            var scenes = new MenuScenes("Title", "Training", "Chapter1", "Arena");

            Assert.That(scenes.MainMenu, Is.EqualTo("Title"));
            Assert.That(scenes.Range, Is.EqualTo("Training"));
            Assert.That(scenes.Campaign, Is.EqualTo("Chapter1"));
            Assert.That(scenes.Net, Is.EqualTo("Arena"));
            Assert.That(scenes.ToString(), Is.EqualTo("mainMenu=Title range=Training campaign=Chapter1 net=Arena"));
        }

        [Test]
        public void ABlankNameKeepsTheDefault()
        {
            var scenes = new MenuScenes(null, "", " ", " Chapter1 ");

            Assert.That(scenes.MainMenu, Is.EqualTo(MenuRoute.MainMenuSceneName));
            Assert.That(scenes.Range, Is.EqualTo(MenuRoute.RangeSceneName));
            Assert.That(scenes.Campaign, Is.EqualTo(MenuRoute.CampaignSceneName));
            Assert.That(scenes.Net, Is.EqualTo("Chapter1"), "surrounding spaces are dropped");
        }

        [TestCase("Levels/Chapter1")]
        [TestCase("Levels\\Chapter1")]
        [TestCase("Chapter1.unity")]
        [TestCase("Chapter1.UNITY")]
        public void RejectsANameThatIsNotAPlainSceneName(string name)
        {
            var e = Assert.Throws<ArgumentException>(() => new MenuScenes(null, null, name, null));

            Assert.That(e.ParamName, Is.EqualTo("campaign"));
            Assert.That(e.Message, Does.Contain(name));
        }

        [Test]
        public void PlanLoadsTheConfiguredScenes()
        {
            var scenes = new MenuScenes("Title", "Training", "Chapter1", "Arena");

            Assert.That(MenuRoute.Plan(MenuDestination.Campaign, _root, scenes).ScenePath, Is.EqualTo("Assets/game/Scenes/Chapter1.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Range, _root, scenes).ScenePath, Is.EqualTo("Assets/game/Scenes/Training.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Tutorial, _root, scenes).ScenePath, Is.EqualTo("Assets/game/Scenes/Training.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Tutorial, _root, scenes).StartsTutorial, Is.True);
            Assert.That(MenuRoute.BackToMenu(_root, scenes).ScenePath, Is.EqualTo("Assets/game/Scenes/Title.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Quit, _root, scenes).Kind, Is.EqualTo(MenuTransitionKind.Quit));

            foreach (var online in new[] { MenuDestination.CreateLobby, MenuDestination.JoinLobby, MenuDestination.QuickMatch })
            {
                var transition = MenuRoute.Plan(online, _root, scenes);
                Assert.That(transition.ScenePath, Is.EqualTo("Assets/game/Scenes/Arena.unity"), online.ToString());
                Assert.That(transition.Lobby.IsRequested, Is.True, online.ToString());
            }
        }

        [Test]
        public void TheConfiguredScenesFollowTheSceneRoot()
        {
            var scenes = new MenuScenes(null, null, "Chapter1", null);

            Assert.That(MenuRoute.Plan(MenuDestination.Campaign, "Packages/com.example.game", scenes).ScenePath,
                Is.EqualTo("Packages/com.example.game/Scenes/Chapter1.unity"));
            Assert.That(MenuRoute.CampaignSceneUnder("Packages/com.example.game", scenes),
                Is.EqualTo("Packages/com.example.game/Scenes/Chapter1.unity"));
        }

        [Test]
        public void WithoutDataTheRoutesAreTheDefaults()
        {
            foreach (var destination in MenuRoute.Destinations)
            {
                Assert.That(
                    MenuRoute.Plan(destination, _root, MenuScenes.Default).ScenePath,
                    Is.EqualTo(MenuRoute.Plan(destination).ScenePath),
                    destination.ToString());
            }

            Assert.That(MenuRoute.Plan(MenuDestination.Campaign, _root, MenuScenes.Default).ScenePath, Is.EqualTo(MenuRoute.CampaignScene));
            Assert.That(MenuRoute.BackToMenu(_root, MenuScenes.Default).ScenePath, Is.EqualTo(MenuRoute.MainMenuScene));
            Assert.That(MenuRoute.NetSceneUnder(_root, MenuScenes.Default), Is.EqualTo(MenuRoute.NetScene));
            Assert.That(MenuRoute.RangeSceneUnder(_root, MenuScenes.Default), Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(MenuRoute.MainMenuSceneUnder(_root, MenuScenes.Default), Is.EqualTo(MenuRoute.MainMenuScene));
        }

        [Test]
        public void RejectsMissingScenes()
        {
            Assert.Throws<ArgumentNullException>(() => MenuRoute.Plan(MenuDestination.Campaign, _root, null));
            Assert.Throws<ArgumentNullException>(() => MenuRoute.Plan(MenuDestination.Quit, _root, null));
            Assert.Throws<ArgumentNullException>(() => MenuRoute.BackToMenu(_root, null));
            Assert.Throws<ArgumentNullException>(() => MenuRoute.NetSceneUnder(_root, null));
        }
    }
}
