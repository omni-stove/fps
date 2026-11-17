using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class DemoSampleRootTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        private static string Root => DemoSample.Root;

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
        public void TheSampleRootIsTheDefaultOutput()
        {
            Assert.That(Root, Is.Not.Null.And.StartsWith("Assets/"));
            Assert.That(ShooterKitRoot.DefaultOutput, Is.EqualTo(Root));
            Assert.That(ShooterKitRoot.Output, Is.EqualTo(Root));
        }

        [TestCase("Assets/Samples/ShooterKitDemo/Scripts/Editor/Shooter.Samples.Editor.asmdef", "Assets/Samples/ShooterKitDemo")]
        [TestCase("Assets/Samples/Shooter Kit/0.1.0/Demo Game/Scripts/Editor/Shooter.Samples.Editor.asmdef", "Assets/Samples/Shooter Kit/0.1.0/Demo Game")]
        [TestCase("Packages/com.omnistove.shooter-kit/Scripts/Editor/Shooter.Samples.Editor.asmdef", null)]
        [TestCase("Scripts/Editor/Shooter.Samples.Editor.asmdef", null)]
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
            Assert.That(GameRulesAssetBuilder.CaptureTheFlagPath, Is.EqualTo(Root + "/Rules/CaptureTheFlag.asset"));
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

            Assert.That(TraversalFeatureSceneBuilder.RulesPath(TraversalFeature.Swim), Is.EqualTo(Root + "/Rules/Features/Swim.asset"));
            Assert.That(TraversalFeatureSceneBuilder.MotorSettingsPath, Is.EqualTo(Root + "/Movement/Feature_DoubleJump.asset"));
            Assert.That(TraversalFeatureSceneBuilder.TraversalTuningPath, Is.EqualTo(Root + "/Tuning/Feature_WallRun.json"));
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
            Assert.That(BuildSceneList.CaptureTheFlagScene, Is.EqualTo(Root + "/Scenes/CaptureTheFlag.unity"));
            Assert.That(BuildSceneList.HordeScene, Is.EqualTo(Root + "/Scenes/Horde.unity"));
            Assert.That(BuildSceneList.BattleRoyaleScene, Is.EqualTo(Root + "/Scenes/BattleRoyale.unity"));
            Assert.That(BuildSceneList.ModeTestScene, Is.EqualTo(Root + "/Scenes/ModeTest.unity"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo(Root + "/Scenes/NetTest.unity"));
            Assert.That(BuildSceneList.TraversalFeatureScene(TraversalFeature.Lean), Is.EqualTo(Root + "/Scenes/Feature_Lean.unity"));
            Assert.That(BuildSceneList.TraversalFeatureScene(TraversalFeature.Swim), Is.EqualTo(Root + "/Scenes/Feature_Swim.unity"));
            Assert.That(BuildSceneList.TraversalFeatureScene(TraversalFeature.WallRun), Is.EqualTo(Root + "/Scenes/Feature_WallRun.unity"));
            Assert.That(BuildSceneList.TraversalFeatureScene(TraversalFeature.DoubleJump), Is.EqualTo(Root + "/Scenes/Feature_DoubleJump.unity"));
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { Root + "/Scenes/NetTest.unity", Root + "/Scenes/NetArena01.unity" }));
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
            ShooterKitRoot.SetPreferredOutput("Assets/ShooterKit/");

            Assert.That(ShooterKitRoot.Output, Is.EqualTo("Assets/ShooterKit"));
            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo("Assets/ShooterKit/Weapons/Rifle.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo("Assets/ShooterKit/Rules/Range.asset"));
            Assert.That(BuildSceneList.MainMenuScene, Is.EqualTo("Assets/ShooterKit/Scenes/MainMenu.unity"));
            Assert.That(CampaignSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/ShooterKit/Scenes/Campaign01/NavMesh-Level.asset"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo("Assets/ShooterKit/Scenes/NetTest.unity"));
            Assert.That(BuildSceneList.TraversalFeatureScene(TraversalFeature.WallRun), Is.EqualTo("Assets/ShooterKit/Scenes/Feature_WallRun.unity"));
            Assert.That(TraversalFeatureSceneBuilder.TraversalTuningPath, Is.EqualTo("Assets/ShooterKit/Tuning/Feature_WallRun.json"));

            ShooterKitRoot.ClearPreferredOutput();

            Assert.That(ShooterKitRoot.Output, Is.EqualTo(Root));
        }
    }
}
