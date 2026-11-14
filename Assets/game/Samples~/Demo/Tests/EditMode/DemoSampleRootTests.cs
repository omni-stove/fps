using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    /// <summary>サンプルが自分のフォルダ (<see cref="DemoSample.Root"/>) を出力先ルートの既定にし、ビルダーがその下に書くことを確かめる。</summary>
    public sealed class DemoSampleRootTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        private static string Root => DemoSample.Root;

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
        public void TheSampleRootIsTheDefaultOutput()
        {
            Assert.That(Root, Is.Not.Null.And.StartsWith("Assets/"));
            Assert.That(FpsKitRoot.DefaultOutput, Is.EqualTo(Root));
            Assert.That(FpsKitRoot.Output, Is.EqualTo(Root));
        }

        [TestCase("Assets/Samples/FpsKitDemo/Scripts/Editor/Fps.Samples.Editor.asmdef", "Assets/Samples/FpsKitDemo")]
        [TestCase("Assets/Samples/FPS Kit/0.1.0/Demo Game/Scripts/Editor/Fps.Samples.Editor.asmdef", "Assets/Samples/FPS Kit/0.1.0/Demo Game")]
        [TestCase("Packages/com.omnistove.fps/Scripts/Editor/Fps.Samples.Editor.asmdef", null)]
        [TestCase("Scripts/Editor/Fps.Samples.Editor.asmdef", null)]
        [TestCase(null, null)]
        public void RootOfIsTwoFoldersAboveTheAsmdef(string asmdefPath, string expected)
        {
            Assert.That(DemoSample.RootOf(asmdefPath), Is.EqualTo(expected));
        }

        [Test]
        public void GeneratedAssetPathsAreUnderTheSampleByDefault()
        {
            Assert.That(GameRulesAssetBuilder.Folder, Is.EqualTo(Root + "/Rules"));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchPath, Is.EqualTo(Root + "/Rules/TeamDeathmatch.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignEasyPath, Is.EqualTo(Root + "/Rules/CampaignEasy.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignNormalPath, Is.EqualTo(Root + "/Rules/CampaignNormal.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignHardPath, Is.EqualTo(Root + "/Rules/CampaignHard.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo(Root + "/Rules/Range.asset"));
            Assert.That(GameRulesAssetBuilder.TpsRangePath, Is.EqualTo(Root + "/Rules/TpsRange.asset"));
            Assert.That(GameRulesAssetBuilder.DominationPath, Is.EqualTo(Root + "/Rules/Domination.asset"));
            Assert.That(GameRulesAssetBuilder.FreeForAllPath, Is.EqualTo(Root + "/Rules/FreeForAll.asset"));
            Assert.That(GameRulesAssetBuilder.BattleRoyalePath, Is.EqualTo(Root + "/Rules/BattleRoyale.asset"));

            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo(Root + "/Weapons/Rifle.asset"));
            Assert.That(WeaponAssetBuilder.HandgunPath, Is.EqualTo(Root + "/Weapons/Handgun.asset"));
            Assert.That(WeaponAssetBuilder.CatalogPath, Is.EqualTo(Root + "/Weapons/WeaponCatalog.asset"));
            Assert.That(WeaponAssetBuilder.LoadoutPath, Is.EqualTo(Root + "/Weapons/DefaultLoadout.asset"));
            Assert.That(WeaponAssetBuilder.LauncherPath, Is.EqualTo(Root + "/Weapons/Launcher.asset"));
            Assert.That(GunGameSceneBuilder.LadderPath, Is.EqualTo(Root + "/Weapons/GunGameLadder.asset"));

            Assert.That(PresentationAssetBuilder.AudioFolder, Is.EqualTo(Root + "/Audio"));
            Assert.That(PresentationAssetBuilder.PresentationFolder, Is.EqualTo(Root + "/Presentation"));

            Assert.That(MissionCatalogBuilder.CatalogPath, Is.EqualTo(Root + "/Missions/MissionCatalog.asset"));
            Assert.That(MissionTestSceneBuilder.DefinitionPath, Is.EqualTo(Root + "/Missions/TestMission.asset"));
            Assert.That(CampaignSceneBuilder.DefinitionPath, Is.EqualTo(Root + "/Missions/Campaign01.asset"));
            Assert.That(Campaign02Mission.DefinitionPath, Is.EqualTo(Root + "/Missions/Campaign02.asset"));
        }

        [Test]
        public void ScenePathsAreUnderTheSampleByDefault()
        {
            Assert.That(BuildSceneList.MainMenuScene, Is.EqualTo(Root + "/Scenes/MainMenu.unity"));
            Assert.That(BuildSceneList.BootstrapScene, Is.EqualTo(Root + "/Scenes/Bootstrap.unity"));
            Assert.That(BuildSceneList.EnemyAiTestScene, Is.EqualTo(Root + "/Scenes/EnemyAiTest.unity"));
            Assert.That(BuildSceneList.MissionTestScene, Is.EqualTo(Root + "/Scenes/MissionTest.unity"));
            Assert.That(BuildSceneList.DestructionTestScene, Is.EqualTo(Root + "/Scenes/DestructionTest.unity"));
            Assert.That(BuildSceneList.CampaignScene, Is.EqualTo(Root + "/Scenes/Campaign01.unity"));
            Assert.That(BuildSceneList.Campaign02Scene, Is.EqualTo(Root + "/Scenes/Campaign02.unity"));
            Assert.That(BuildSceneList.WaveTestScene, Is.EqualTo(Root + "/Scenes/WaveTest.unity"));
            Assert.That(BuildSceneList.FreeForAllScene, Is.EqualTo(Root + "/Scenes/FreeForAll.unity"));
            Assert.That(BuildSceneList.KillConfirmedScene, Is.EqualTo(Root + "/Scenes/KillConfirmed.unity"));
            Assert.That(BuildSceneList.GunGameScene, Is.EqualTo(Root + "/Scenes/GunGame.unity"));
            Assert.That(BuildSceneList.HardpointScene, Is.EqualTo(Root + "/Scenes/Hardpoint.unity"));
            Assert.That(BuildSceneList.GuardianScene, Is.EqualTo(Root + "/Scenes/Guardian.unity"));
            Assert.That(BuildSceneList.ExecutionScene, Is.EqualTo(Root + "/Scenes/Execution.unity"));
            Assert.That(BuildSceneList.SearchAndDestroyScene, Is.EqualTo(Root + "/Scenes/SearchAndDestroy.unity"));
            Assert.That(BuildSceneList.HordeScene, Is.EqualTo(Root + "/Scenes/Horde.unity"));
            Assert.That(BuildSceneList.BattleRoyaleScene, Is.EqualTo(Root + "/Scenes/BattleRoyale.unity"));
            Assert.That(BuildSceneList.ModeTestScene, Is.EqualTo(Root + "/Scenes/ModeTest.unity"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo(Root + "/Scenes/NetTest.unity"));
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { Root + "/Scenes/NetTest.unity", Root + "/Scenes/NetArena01.unity" }));
            Assert.That(
                BuildSceneList.NetTestScene,
                Is.EqualTo(MenuRoute.NetSceneUnder(MenuSceneSettings.LoadSceneRoot(), MenuSceneSettings.LoadScenes())),
                "the builder saves where the runtime loads");
        }

        [Test]
        public void NavMeshPathsAreUnderTheSampleByDefault()
        {
            Assert.That(EnemyTestSceneBuilder.NavMeshDataPath, Is.EqualTo(Root + "/Scenes/EnemyAiTest/NavMesh-Floor.asset"));
            Assert.That(DestructionTestSceneBuilder.NavMeshDataPath, Is.EqualTo(Root + "/Scenes/DestructionTest/NavMesh-Floor.asset"));
            Assert.That(WaveTestSceneBuilder.NavMeshDataPath, Is.EqualTo(Root + "/Scenes/WaveTest/NavMesh-Floor.asset"));
            Assert.That(CampaignSceneBuilder.NavMeshDataPath, Is.EqualTo(Root + "/Scenes/Campaign01/NavMesh-Level.asset"));
            Assert.That(Campaign02Mission.NavMeshDataPath, Is.EqualTo(Root + "/Scenes/Campaign02/NavMesh-Level.asset"));
        }

        [Test]
        public void PreferenceMovesTheSamplePaths()
        {
            FpsKitRoot.SetPreferredOutput("Assets/FpsKit/");

            Assert.That(FpsKitRoot.Output, Is.EqualTo("Assets/FpsKit"));
            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo("Assets/FpsKit/Weapons/Rifle.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo("Assets/FpsKit/Rules/Range.asset"));
            Assert.That(BuildSceneList.MainMenuScene, Is.EqualTo("Assets/FpsKit/Scenes/MainMenu.unity"));
            Assert.That(CampaignSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/FpsKit/Scenes/Campaign01/NavMesh-Level.asset"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo("Assets/FpsKit/Scenes/NetTest.unity"));

            FpsKitRoot.ClearPreferredOutput();

            Assert.That(FpsKitRoot.Output, Is.EqualTo(Root));
        }
    }
}
