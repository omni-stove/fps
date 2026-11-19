using Shooter.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Net.Tests
{
    public sealed class NetKitPathsTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        // 開発者が EditorPrefs で出力先を変えていても既定のパスを確かめられるよう、テストの間だけ外して後で戻す。
        [SetUp]
        public void SetUp()
        {
            _hadPreference = EditorPrefs.HasKey(ShooterKitRoot.PrefsKey);
            _savedPreference = EditorPrefs.GetString(ShooterKitRoot.PrefsKey, string.Empty);
            EditorPrefs.DeleteKey(ShooterKitRoot.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPreference)
            {
                EditorPrefs.SetString(ShooterKitRoot.PrefsKey, _savedPreference);
            }
            else
            {
                EditorPrefs.DeleteKey(ShooterKitRoot.PrefsKey);
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
            ShooterKitRoot.SetPreferredOutput("Assets/ShooterKit");

            Assert.That(NetSceneBuilder.ScenePath, Is.EqualTo("Assets/ShooterKit/Scenes/NetTest.unity"));
            Assert.That(NetSceneBuilder.MatchDefinitionPath, Is.EqualTo("Assets/ShooterKit/Rules/NetMatch.asset"));
            Assert.That(NetSceneBuilder.BattleRoyaleScenePath, Is.EqualTo("Assets/ShooterKit/Scenes/NetBattleRoyale.unity"));
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[]
            {
                "Assets/ShooterKit/Scenes/NetTest.unity",
                "Assets/ShooterKit/Scenes/NetArena01.unity",
                "Assets/ShooterKit/Scenes/NetBattleRoyale.unity",
            }));
        }
    }
}
