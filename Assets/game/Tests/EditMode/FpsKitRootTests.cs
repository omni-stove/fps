using System;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    public sealed class FpsKitRootTests
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
        public void OutputAndBundledDefaultToTheRepositoryFolder()
        {
            Assert.That(FpsKitRoot.Output, Is.EqualTo("Assets/game"));
            Assert.That(FpsKitRoot.Bundled, Is.EqualTo("Assets/game"), "this repository opens the kit under Assets, not as a package");
            Assert.That(FpsKitRoot.BundledPath("UI/Resources"), Is.EqualTo("Assets/game/UI/Resources"));
        }

        [Test]
        public void GeneratedAssetPathsAreUnchangedByDefault()
        {
            Assert.That(GameRulesAssetBuilder.Folder, Is.EqualTo("Assets/game/Rules"));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchPath, Is.EqualTo("Assets/game/Rules/TeamDeathmatch.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignEasyPath, Is.EqualTo("Assets/game/Rules/CampaignEasy.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignNormalPath, Is.EqualTo("Assets/game/Rules/CampaignNormal.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignHardPath, Is.EqualTo("Assets/game/Rules/CampaignHard.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo("Assets/game/Rules/Range.asset"));
            Assert.That(GameRulesAssetBuilder.TpsRangePath, Is.EqualTo("Assets/game/Rules/TpsRange.asset"));
            Assert.That(GameRulesAssetBuilder.DominationPath, Is.EqualTo("Assets/game/Rules/Domination.asset"));
            Assert.That(GameRulesAssetBuilder.FreeForAllPath, Is.EqualTo("Assets/game/Rules/FreeForAll.asset"));
            Assert.That(GameRulesAssetBuilder.BattleRoyalePath, Is.EqualTo("Assets/game/Rules/BattleRoyale.asset"));

            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo("Assets/game/Weapons/Rifle.asset"));
            Assert.That(WeaponAssetBuilder.HandgunPath, Is.EqualTo("Assets/game/Weapons/Handgun.asset"));
            Assert.That(WeaponAssetBuilder.CatalogPath, Is.EqualTo("Assets/game/Weapons/WeaponCatalog.asset"));
            Assert.That(WeaponAssetBuilder.LoadoutPath, Is.EqualTo("Assets/game/Weapons/DefaultLoadout.asset"));
            Assert.That(WeaponAssetBuilder.LauncherPath, Is.EqualTo("Assets/game/Weapons/Launcher.asset"));
            Assert.That(GunGameSceneBuilder.LadderPath, Is.EqualTo("Assets/game/Weapons/GunGameLadder.asset"));

            Assert.That(CharacterRigBuilder.Folder, Is.EqualTo("Assets/game/Characters"));
            Assert.That(CharacterRigBuilder.ControllerPath, Is.EqualTo("Assets/game/Characters/CharacterRig.controller"));
            Assert.That(CharacterRigBuilder.ClipFolder, Is.EqualTo("Assets/game/Characters/Clips"));
            Assert.That(PresentationAssetBuilder.AudioFolder, Is.EqualTo("Assets/game/Audio"));
            Assert.That(PresentationAssetBuilder.PresentationFolder, Is.EqualTo("Assets/game/Presentation"));

            Assert.That(MissionCatalogBuilder.CatalogPath, Is.EqualTo("Assets/game/Missions/MissionCatalog.asset"));
            Assert.That(MissionTestSceneBuilder.DefinitionPath, Is.EqualTo("Assets/game/Missions/TestMission.asset"));
            Assert.That(CampaignSceneBuilder.DefinitionPath, Is.EqualTo("Assets/game/Missions/Campaign01.asset"));
            Assert.That(Campaign02Mission.DefinitionPath, Is.EqualTo("Assets/game/Missions/Campaign02.asset"));
        }

        [Test]
        public void ScenePathsAreUnchangedByDefault()
        {
            Assert.That(BuildSceneList.MainMenuScene, Is.EqualTo("Assets/game/Scenes/MainMenu.unity"));
            Assert.That(BuildSceneList.BootstrapScene, Is.EqualTo("Assets/game/Scenes/Bootstrap.unity"));
            Assert.That(BuildSceneList.EnemyAiTestScene, Is.EqualTo("Assets/game/Scenes/EnemyAiTest.unity"));
            Assert.That(BuildSceneList.MissionTestScene, Is.EqualTo("Assets/game/Scenes/MissionTest.unity"));
            Assert.That(BuildSceneList.DestructionTestScene, Is.EqualTo("Assets/game/Scenes/DestructionTest.unity"));
            Assert.That(BuildSceneList.CampaignScene, Is.EqualTo("Assets/game/Scenes/Campaign01.unity"));
            Assert.That(BuildSceneList.Campaign02Scene, Is.EqualTo("Assets/game/Scenes/Campaign02.unity"));
            Assert.That(BuildSceneList.WaveTestScene, Is.EqualTo("Assets/game/Scenes/WaveTest.unity"));
            Assert.That(BuildSceneList.FreeForAllScene, Is.EqualTo("Assets/game/Scenes/FreeForAll.unity"));
            Assert.That(BuildSceneList.KillConfirmedScene, Is.EqualTo("Assets/game/Scenes/KillConfirmed.unity"));
            Assert.That(BuildSceneList.GunGameScene, Is.EqualTo("Assets/game/Scenes/GunGame.unity"));
            Assert.That(BuildSceneList.HardpointScene, Is.EqualTo("Assets/game/Scenes/Hardpoint.unity"));
            Assert.That(BuildSceneList.GuardianScene, Is.EqualTo("Assets/game/Scenes/Guardian.unity"));
            Assert.That(BuildSceneList.ExecutionScene, Is.EqualTo("Assets/game/Scenes/Execution.unity"));
            Assert.That(BuildSceneList.SearchAndDestroyScene, Is.EqualTo("Assets/game/Scenes/SearchAndDestroy.unity"));
            Assert.That(BuildSceneList.HordeScene, Is.EqualTo("Assets/game/Scenes/Horde.unity"));
            Assert.That(BuildSceneList.BattleRoyaleScene, Is.EqualTo("Assets/game/Scenes/BattleRoyale.unity"));
            Assert.That(BuildSceneList.ModeTestScene, Is.EqualTo("Assets/game/Scenes/ModeTest.unity"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo("Assets/game/Scenes/NetTest.unity"));
            Assert.That(BuildSceneList.ServerScenes, Is.EqualTo(new[] { "Assets/game/Scenes/NetTest.unity" }));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo(MenuRoute.NetScene), "the builder saves where the runtime loads");
        }

        [Test]
        public void NavMeshPathsAreUnchangedByDefault()
        {
            Assert.That(EnemyTestSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/EnemyAiTest/NavMesh-Floor.asset"));
            Assert.That(DestructionTestSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/DestructionTest/NavMesh-Floor.asset"));
            Assert.That(WaveTestSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/WaveTest/NavMesh-Floor.asset"));
            Assert.That(CampaignSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Campaign01/NavMesh-Level.asset"));
            Assert.That(Campaign02Mission.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Campaign02/NavMesh-Level.asset"));
        }

        [Test]
        public void PreferenceMovesEveryGeneratedPath()
        {
            FpsKitRoot.SetPreferredOutput("Assets/FpsKit/");

            Assert.That(FpsKitRoot.Output, Is.EqualTo("Assets/FpsKit"));
            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo("Assets/FpsKit/Weapons/Rifle.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo("Assets/FpsKit/Rules/Range.asset"));
            Assert.That(BuildSceneList.MainMenuScene, Is.EqualTo("Assets/FpsKit/Scenes/MainMenu.unity"));
            Assert.That(CampaignSceneBuilder.NavMeshDataPath, Is.EqualTo("Assets/FpsKit/Scenes/Campaign01/NavMesh-Level.asset"));
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo("Assets/FpsKit/Scenes/NetTest.unity"));
            Assert.That(FpsKitRoot.Bundled, Is.EqualTo("Assets/game"), "the bundled source does not follow the output root");

            FpsKitRoot.ClearPreferredOutput();

            Assert.That(FpsKitRoot.Output, Is.EqualTo(KitPaths.DefaultRoot));
        }

        [Test]
        public void SetPreferredOutputRejectsReadOnlyRootsWithoutSavingThem()
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.SetPreferredOutput("Packages/com.omnistove.fps"));
            Assert.That(EditorPrefs.HasKey(FpsKitRoot.PrefsKey), Is.False);
        }

        [Test]
        public void ResolveOutputPrefersTheArgumentOverThePreference()
        {
            var args = new[] { "Unity", "-batchmode", FpsKitRoot.ArgumentName, "Assets/FromArgs", "-quit" };

            Assert.That(FpsKitRoot.ResolveOutput(args, "Assets/FromPrefs"), Is.EqualTo("Assets/FromArgs"));
            Assert.That(FpsKitRoot.ResolveOutput(new[] { "Unity", "-batchmode" }, "Assets/FromPrefs"), Is.EqualTo("Assets/FromPrefs"));
            Assert.That(FpsKitRoot.ResolveOutput(new[] { "Unity" }, string.Empty), Is.EqualTo(KitPaths.DefaultRoot));
            Assert.That(FpsKitRoot.ResolveOutput(null, null), Is.EqualTo(KitPaths.DefaultRoot));
        }

        [Test]
        public void ResolveOutputNormalizesTheRoot()
        {
            var args = new[] { FpsKitRoot.ArgumentName, "Assets\\Kit\\" };

            Assert.That(FpsKitRoot.ResolveOutput(args, null), Is.EqualTo("Assets/Kit"));
        }

        [Test]
        public void ResolveOutputRejectsAMissingOrRepeatedArgument()
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName }, null));
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, "-quit" }, null));
            Assert.Throws<ArgumentException>(
                () => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, "Assets/A", FpsKitRoot.ArgumentName, "Assets/B" }, null));
        }

        [TestCase("Packages/com.omnistove.fps")]
        [TestCase("Assets")]
        [TestCase("ProjectSettings")]
        [TestCase("Assets/../Packages")]
        public void ResolveOutputRejectsRootsOutsideAssets(string root)
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, root }, null));
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(null, root));
        }

        [Test]
        public void ResolveBundledUsesThePackageWhenThereIsOne()
        {
            Assert.That(FpsKitRoot.ResolveBundled("Packages/com.omnistove.fps"), Is.EqualTo("Packages/com.omnistove.fps"));
            Assert.That(FpsKitRoot.ResolveBundled(null), Is.EqualTo(KitPaths.DefaultRoot));
            Assert.That(FpsKitRoot.ResolveBundled(string.Empty), Is.EqualTo(KitPaths.DefaultRoot));
        }
    }
}
