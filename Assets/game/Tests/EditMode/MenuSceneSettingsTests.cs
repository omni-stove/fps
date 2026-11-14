using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    public sealed class MenuSceneSettingsTests
    {
        private MenuSceneSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<MenuSceneSettings>();
            _settings.name = "GameMenuScenes";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ANewAssetStartsWithTheDefaultNames()
        {
            Assert.That(_settings.MainMenu, Is.EqualTo(MenuRoute.MainMenuSceneName));
            Assert.That(_settings.Range, Is.EqualTo(MenuRoute.RangeSceneName));
            Assert.That(_settings.Campaign, Is.EqualTo(MenuRoute.CampaignSceneName));
            Assert.That(_settings.Net, Is.EqualTo(MenuRoute.NetSceneName));
            Assert.That(_settings.ToScenes().ToString(), Is.EqualTo(MenuScenes.Default.ToString()));
        }

        [Test]
        public void TheAssetOverridesTheCampaignScene()
        {
            _settings.Campaign = "Chapter1";

            var scenes = _settings.ToScenes();

            Assert.That(scenes.Campaign, Is.EqualTo("Chapter1"));
            Assert.That(scenes.MainMenu, Is.EqualTo(MenuRoute.MainMenuSceneName));
            Assert.That(MenuRoute.Plan(MenuDestination.Campaign, KitPaths.DefaultRoot, scenes).ScenePath,
                Is.EqualTo("Assets/game/Scenes/Chapter1.unity"));
        }

        [Test]
        public void AnEmptyFieldFallsBackToTheDefault()
        {
            _settings.MainMenu = "";
            _settings.Net = null;

            var scenes = _settings.ToScenes();

            Assert.That(scenes.MainMenu, Is.EqualTo(MenuRoute.MainMenuSceneName));
            Assert.That(scenes.Net, Is.EqualTo(MenuRoute.NetSceneName));
        }

        [Test]
        public void AnInvalidNameIsReportedWithTheAssetName()
        {
            _settings.Range = "Levels/Training.unity";

            var e = Assert.Throws<MenuSceneSettingsException>(() => _settings.ToScenes());

            Assert.That(e.Message, Does.StartWith("menu scene settings 'GameMenuScenes': "));
            Assert.That(e.Message, Does.Contain("Levels/Training.unity"));
            Assert.That(e.InnerException, Is.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void ResetToDefaultsRestoresTheNames()
        {
            _settings.MainMenu = "Title";
            _settings.Range = "Training";
            _settings.Campaign = "Chapter1";
            _settings.Net = "Arena";

            _settings.ResetToDefaults();

            Assert.That(_settings.ToScenes().ToString(), Is.EqualTo(MenuScenes.Default.ToString()));
        }

        [Test]
        public void AnEmptySceneRootIsTheKitRoot()
        {
            Assert.That(_settings.ToSceneRoot(), Is.EqualTo(KitPaths.DefaultRoot));

            _settings.SceneRoot = "  ";

            Assert.That(_settings.ToSceneRoot(), Is.EqualTo(KitPaths.DefaultRoot));
        }

        [Test]
        public void TheSceneRootIsNormalizedAndResetClearsIt()
        {
            _settings.SceneRoot = "Assets\\MyGame\\";

            Assert.That(_settings.ToSceneRoot(), Is.EqualTo("Assets/MyGame"));

            _settings.ResetToDefaults();

            Assert.That(_settings.SceneRoot, Is.Empty);
        }

        [Test]
        public void AnInvalidSceneRootIsReportedWithTheAssetName()
        {
            _settings.name = "BrokenRoot";
            _settings.SceneRoot = "Assets/../Elsewhere";

            var e = Assert.Throws<MenuSceneSettingsException>(() => _settings.ToSceneRoot());

            Assert.That(e.Message, Does.Contain("BrokenRoot"));
        }

        [Test]
        public void WithoutAnAssetTheDefaultsAreLoaded()
        {
            Assume.That(MenuSceneSettings.Load(), Is.Null, "this project provides its own Resources/" + MenuSceneSettings.ResourcePath);

            Assert.That(MenuSceneSettings.LoadScenes(), Is.SameAs(MenuScenes.Default));
            Assert.That(MenuSceneSettings.LoadSceneRoot(), Is.EqualTo(KitPaths.DefaultRoot));
        }
    }
}
