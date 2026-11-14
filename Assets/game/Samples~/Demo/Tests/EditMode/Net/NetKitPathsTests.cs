using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Net.Tests
{
    /// <summary>NetSceneBuilder が書く場所と、実行時に NetBootstrap が読む場所が、既定の出力先ルートで今と同じであること。</summary>
    public sealed class NetKitPathsTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        // 開発者が EditorPrefs で出力先を変えていても既定のパスを確かめられるよう、テストの間だけ外して後で戻す。
        [SetUp]
        public void SetUp()
        {
            _hadPreference = EditorPrefs.HasKey(FpsKitRoot.PrefsKey);
            _savedPreference = EditorPrefs.GetString(FpsKitRoot.PrefsKey, string.Empty);
            EditorPrefs.DeleteKey(FpsKitRoot.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPreference)
            {
                EditorPrefs.SetString(FpsKitRoot.PrefsKey, _savedPreference);
            }
            else
            {
                EditorPrefs.DeleteKey(FpsKitRoot.PrefsKey);
            }
        }

        [Test]
        public void TheRuntimeLoadsTheNetSceneFromTheSample()
        {
            Assert.That(
                MenuRoute.NetSceneUnder(MenuSceneSettings.LoadSceneRoot(), MenuSceneSettings.LoadScenes()),
                Is.EqualTo(DemoSample.Root + "/Scenes/NetTest.unity"));
        }

        [Test]
        public void BuilderPathsAreUnchangedByDefault()
        {
            Assert.That(
                NetSceneBuilder.ScenePath,
                Is.EqualTo(MenuRoute.NetSceneUnder(MenuSceneSettings.LoadSceneRoot(), MenuSceneSettings.LoadScenes())),
                "the builder saves where the runtime loads");
            Assert.That(NetSceneBuilder.PlayerPrefabPath, Is.EqualTo(DemoSample.Root + "/Prefabs/Net/NetPlayer.prefab"));
            Assert.That(NetSceneBuilder.MatchPrefabPath, Is.EqualTo(DemoSample.Root + "/Prefabs/Net/NetMatch.prefab"));
        }

        [Test]
        public void BuilderPathsFollowTheOutputRoot()
        {
            FpsKitRoot.SetPreferredOutput("Assets/FpsKit");

            Assert.That(NetSceneBuilder.ScenePath, Is.EqualTo("Assets/FpsKit/Scenes/NetTest.unity"));
            Assert.That(NetSceneBuilder.PlayerPrefabPath, Is.EqualTo("Assets/FpsKit/Prefabs/Net/NetPlayer.prefab"));
            Assert.That(NetSceneBuilder.MatchPrefabPath, Is.EqualTo("Assets/FpsKit/Prefabs/Net/NetMatch.prefab"));
            Assert.That(ServerBuilder.ServerScenePaths(), Is.EqualTo(new[] { "Assets/FpsKit/Scenes/NetTest.unity", "Assets/FpsKit/Scenes/NetArena01.unity" }));
        }
    }
}
