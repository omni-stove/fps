using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Net.Tests
{
    /// <summary>NetSceneBuilder が書く場所が、既定の出力先ルートで今と同じであること。</summary>
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
        public void BuilderPathsAreUnchangedByDefault()
        {
            Assert.That(NetSceneBuilder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/NetTest.unity"));
            Assert.That(NetSceneBuilder.MatchDefinitionPath, Is.EqualTo(DemoSample.Root + "/Rules/NetMatch.asset"));
        }

        [Test]
        public void BuilderPathsFollowTheOutputRoot()
        {
            FpsKitRoot.SetPreferredOutput("Assets/FpsKit");

            Assert.That(NetSceneBuilder.ScenePath, Is.EqualTo("Assets/FpsKit/Scenes/NetTest.unity"));
            Assert.That(NetSceneBuilder.MatchDefinitionPath, Is.EqualTo("Assets/FpsKit/Rules/NetMatch.asset"));
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { "Assets/FpsKit/Scenes/NetTest.unity", "Assets/FpsKit/Scenes/NetArena01.unity" }));
        }
    }
}
