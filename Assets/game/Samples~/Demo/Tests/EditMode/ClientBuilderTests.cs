using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class ClientBuilderTests
    {
        [Test]
        public void ScenesFollowTheListAndSkipMissingOnes()
        {
            var listed = new[] { "Assets/A.unity", "Assets/B.unity", "", "Assets/C.unity", "Assets/A.unity" };
            var skipped = new List<string>();

            var scenes = ClientBuilder.ClientScenePaths(listed, path => path != "Assets/B.unity", skipped);

            Assert.That(scenes, Is.EqualTo(new[] { "Assets/A.unity", "Assets/C.unity" }));
            Assert.That(skipped, Is.EqualTo(new[] { "Assets/B.unity" }));
            Assert.That(ClientBuilder.ClientScenePaths(listed, _ => false, null), Is.Empty);
        }

        [Test]
        public void ScenesRejectNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ClientBuilder.ClientScenePaths(null, _ => true, null));
            Assert.Throws<ArgumentNullException>(() => ClientBuilder.ClientScenePaths(new string[0], null, null));
        }

        [Test]
        public void StartSceneComesFirstInTheBuildSceneList()
        {
            Assert.That(BuildSceneList.Scenes[0], Is.EqualTo(BuildSceneList.MainMenuScene));

            var scenes = ClientBuilder.ClientScenePaths(BuildSceneList.Scenes, _ => true, null);

            Assert.That(scenes, Is.EqualTo(BuildSceneList.Scenes));
        }

        [Test]
        public void WebGLListsOnlyTheMenuCampaignAndRange()
        {
            Assert.That(ClientBuilder.ListedScenes(ClientPlatform.Windows), Is.EqualTo(BuildSceneList.Scenes));
            Assert.That(ClientBuilder.ListedScenes(ClientPlatform.MacOS), Is.EqualTo(BuildSceneList.Scenes));
            var web = ClientBuilder.ListedScenes(ClientPlatform.WebGL);
            Assert.That(web[0], Is.EqualTo(BuildSceneList.MainMenuScene));
            Assert.That(
                web.Select(Path.GetFileName),
                Is.EqualTo(new[] { "MainMenu.unity", "Bootstrap.unity", "Campaign01.unity", "Campaign02.unity" }));
            Assert.That(BuildSceneList.Scenes, Is.SupersetOf(BuildSceneList.WebScenes));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.ListedScenes((ClientPlatform)99));
        }

        [TestCase(ClientPlatform.Windows, BuildTarget.StandaloneWindows64)]
        [TestCase(ClientPlatform.MacOS, BuildTarget.StandaloneOSX)]
        [TestCase(ClientPlatform.WebGL, BuildTarget.WebGL)]
        public void MapsPlatformsToBuildTargets(ClientPlatform platform, BuildTarget expected)
        {
            Assert.That(ClientBuilder.ToBuildTarget(platform), Is.EqualTo(expected));
        }

        [TestCase(ClientPlatform.Windows, BuildTargetGroup.Standalone)]
        [TestCase(ClientPlatform.MacOS, BuildTargetGroup.Standalone)]
        [TestCase(ClientPlatform.WebGL, BuildTargetGroup.WebGL)]
        public void MapsPlatformsToBuildTargetGroups(ClientPlatform platform, BuildTargetGroup expected)
        {
            Assert.That(ClientBuilder.ToBuildTargetGroup(platform), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyWebGLDropsEos()
        {
            Assert.That(ClientBuilder.ExtraScriptingDefines(ClientPlatform.Windows), Is.Empty);
            Assert.That(ClientBuilder.ExtraScriptingDefines(ClientPlatform.MacOS), Is.Empty);
            Assert.That(ClientBuilder.ExtraScriptingDefines(ClientPlatform.WebGL), Is.EqualTo(new[] { "EOS_DISABLE" }));
        }

        [TestCase(ClientPlatform.Windows, "Windows Build Support (Mono)")]
        [TestCase(ClientPlatform.MacOS, "Mac Build Support (Mono)")]
        [TestCase(ClientPlatform.WebGL, "Web Build Support")]
        public void NamesTheModuleToInstall(ClientPlatform platform, string expected)
        {
            Assert.That(ClientBuilder.RequiredModuleName(platform), Is.EqualTo(expected));
        }

        [Test]
        public void RejectsUnknownPlatform()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.ToBuildTarget((ClientPlatform)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.ToBuildTargetGroup((ClientPlatform)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.ExtraScriptingDefines((ClientPlatform)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.RequiredModuleName((ClientPlatform)99));
            Assert.Throws<ArgumentNullException>(() => ClientBuilder.BuildFor(null));
        }
    }
}
