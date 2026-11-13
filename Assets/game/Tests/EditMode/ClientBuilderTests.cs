using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    /// <summary>実際のビルドは <c>make build windows|macos</c> と client-release ワークフローに任せ、入れるシーンとターゲットの決め方だけを見る。</summary>
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

        [TestCase(ClientPlatform.Windows, BuildTarget.StandaloneWindows64)]
        [TestCase(ClientPlatform.MacOS, BuildTarget.StandaloneOSX)]
        public void MapsPlatformsToBuildTargets(ClientPlatform platform, BuildTarget expected)
        {
            Assert.That(ClientBuilder.ToBuildTarget(platform), Is.EqualTo(expected));
        }

        [Test]
        public void RejectsUnknownPlatform()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientBuilder.ToBuildTarget((ClientPlatform)99));
            Assert.Throws<ArgumentNullException>(() => ClientBuilder.BuildFor(null));
        }
    }
}
