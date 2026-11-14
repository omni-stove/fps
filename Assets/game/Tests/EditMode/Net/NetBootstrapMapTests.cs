using System;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary><c>-map</c> の名前から、ビルドに入っているシーンを引く処理 (<see cref="NetBootstrap"/> の .Map.cs)。</summary>
    public sealed class NetBootstrapMapTests
    {
        private static readonly string[] ServerScenes =
        {
            "Assets/game/Scenes/NetTest.unity",
            "Assets/game/Scenes/NetArena01.unity",
        };

        [Test]
        public void FindsTheSceneWhoseFileNameIsTheMap()
        {
            Assert.That(NetBootstrap.TryFindMapScene("NetArena01", ServerScenes, out var path), Is.True);
            Assert.That(path, Is.EqualTo("Assets/game/Scenes/NetArena01.unity"));

            Assert.That(NetBootstrap.TryFindMapScene("NetTest", ServerScenes, out path), Is.True);
            Assert.That(path, Is.EqualTo("Assets/game/Scenes/NetTest.unity"));
        }

        [Test]
        public void DoesNotFindAMissingOrDifferentlyCasedMap()
        {
            Assert.That(NetBootstrap.TryFindMapScene("Missing", ServerScenes, out var path), Is.False);
            Assert.That(path, Is.Null);

            Assert.That(NetBootstrap.TryFindMapScene("netarena01", ServerScenes, out path), Is.False, "scene names are matched exactly");
            Assert.That(NetBootstrap.TryFindMapScene("NetArena", ServerScenes, out path), Is.False, "no prefix match");
            Assert.That(NetBootstrap.TryFindMapScene("", ServerScenes, out path), Is.False);
            Assert.That(NetBootstrap.TryFindMapScene(null, ServerScenes, out path), Is.False);
        }

        [Test]
        public void IgnoresEmptyEntriesAndNonScenePaths()
        {
            var scenes = new[] { "", null, "Assets/game/Scenes/NetArena01.prefab", "Assets\\game\\Scenes\\NetArena01.unity" };

            Assert.That(NetBootstrap.TryFindMapScene("NetArena01", scenes, out var path), Is.True);
            Assert.That(path, Is.EqualTo("Assets\\game\\Scenes\\NetArena01.unity"));
            Assert.Throws<ArgumentNullException>(() => NetBootstrap.TryFindMapScene("NetArena01", null, out _));
        }

        [Test]
        public void MissingMapMessageNamesTheMapAndTheScenesInTheBuild()
        {
            var message = NetBootstrap.MapNotInBuildMessage("Missing", ServerScenes);

            Assert.That(message, Does.StartWith("[Net] -map Missing is not a scene in this build"));
            Assert.That(message, Does.Contain("scenes: NetTest, NetArena01"));
            Assert.That(NetBootstrap.MapNotInBuildMessage("Missing", Array.Empty<string>()), Does.Contain("scenes: none"));
            Assert.Throws<ArgumentNullException>(() => NetBootstrap.MapNotInBuildMessage("Missing", null));
        }
    }
}
