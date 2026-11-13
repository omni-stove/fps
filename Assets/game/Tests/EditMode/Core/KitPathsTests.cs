using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KitPathsTests
    {
        private const string PackageRoot = "Packages/com.omnistove.fps";

        [Test]
        public void DefaultRootIsTheRepositoryFolder()
        {
            Assert.That(KitPaths.NormalizeRoot(KitPaths.DefaultRoot), Is.EqualTo("Assets/game"));
            Assert.That(KitPaths.Combine(KitPaths.DefaultRoot, KitPaths.ScenesFolderName), Is.EqualTo(KitPaths.DefaultScenesFolder));
        }

        [Test]
        public void DefaultMenuScenesAreUnchanged()
        {
            Assert.That(MenuRoute.BackToMenu().ScenePath, Is.EqualTo("Assets/game/Scenes/MainMenu.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Range).ScenePath, Is.EqualTo("Assets/game/Scenes/Bootstrap.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Campaign).ScenePath, Is.EqualTo("Assets/game/Scenes/Campaign01.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.CreateLobby).ScenePath, Is.EqualTo("Assets/game/Scenes/NetTest.unity"));
        }

        [Test]
        public void SceneUnderTheDefaultRootMatchesTheConstants()
        {
            Assert.That(MenuRoute.MainMenuSceneUnder(KitPaths.DefaultRoot), Is.EqualTo(MenuRoute.MainMenuScene));
            Assert.That(MenuRoute.RangeSceneUnder(KitPaths.DefaultRoot), Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(MenuRoute.CampaignSceneUnder(KitPaths.DefaultRoot), Is.EqualTo(MenuRoute.CampaignScene));
            Assert.That(MenuRoute.NetSceneUnder(KitPaths.DefaultRoot), Is.EqualTo(MenuRoute.NetScene));
        }

        [Test]
        public void PlanWithoutARootUsesTheDefaultRoot()
        {
            foreach (var destination in MenuRoute.Destinations)
            {
                Assert.That(
                    MenuRoute.Plan(destination).ScenePath,
                    Is.EqualTo(MenuRoute.Plan(destination, KitPaths.DefaultRoot).ScenePath),
                    destination.ToString());
            }

            Assert.That(MenuRoute.BackToMenu().ScenePath, Is.EqualTo(MenuRoute.BackToMenu(KitPaths.DefaultRoot).ScenePath));
        }

        [Test]
        public void PlanWithARootLoadsScenesUnderIt()
        {
            Assert.That(MenuRoute.Plan(MenuDestination.Campaign, PackageRoot).ScenePath, Is.EqualTo(PackageRoot + "/Scenes/Campaign01.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Range, PackageRoot).ScenePath, Is.EqualTo(PackageRoot + "/Scenes/Bootstrap.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.Tutorial, PackageRoot).StartsTutorial, Is.True);
            Assert.That(MenuRoute.Plan(MenuDestination.QuickMatch, PackageRoot).ScenePath, Is.EqualTo(PackageRoot + "/Scenes/NetTest.unity"));
            Assert.That(MenuRoute.Plan(MenuDestination.QuickMatch, PackageRoot).Lobby.IsRequested, Is.True);
            Assert.That(MenuRoute.BackToMenu(PackageRoot).ScenePath, Is.EqualTo(PackageRoot + "/Scenes/MainMenu.unity"));
        }

        [Test]
        public void QuitIgnoresTheRoot()
        {
            Assert.That(MenuRoute.Plan(MenuDestination.Quit, PackageRoot).Kind, Is.EqualTo(MenuTransitionKind.Quit));
        }

        [Test]
        public void NormalizeRootUnifiesSeparatorsAndDropsTheTrailingSlash()
        {
            Assert.That(KitPaths.NormalizeRoot(" Assets\\Kit/ "), Is.EqualTo("Assets/Kit"));
            Assert.That(KitPaths.NormalizeRoot("Assets/game//"), Is.EqualTo("Assets/game"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        [TestCase("/Assets/game")]
        [TestCase("Assets//game")]
        [TestCase("Assets/../game")]
        [TestCase("Assets/./game")]
        public void NormalizeRootRejectsBrokenRoots(string root)
        {
            Assert.Throws<ArgumentException>(() => KitPaths.NormalizeRoot(root));
        }

        [Test]
        public void CombinePutsTheRelativePathUnderTheRoot()
        {
            Assert.That(KitPaths.Combine("Assets/game/", "Weapons/Rifle.asset"), Is.EqualTo("Assets/game/Weapons/Rifle.asset"));
            Assert.That(KitPaths.Combine("Assets/game", "/Rules\\Range.asset"), Is.EqualTo("Assets/game/Rules/Range.asset"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("../Rules")]
        [TestCase("Rules//Range.asset")]
        public void CombineRejectsBrokenRelativePaths(string relative)
        {
            Assert.Throws<ArgumentException>(() => KitPaths.Combine(KitPaths.DefaultRoot, relative));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Sub/Scene")]
        [TestCase("Sub\\Scene")]
        public void ScenePathRejectsNamesThatAreNotFileNames(string sceneName)
        {
            Assert.Throws<ArgumentException>(() => KitPaths.ScenePath(KitPaths.DefaultRoot, sceneName));
        }

        [Test]
        public void PlanRejectsAnEmptyRoot()
        {
            Assert.Throws<ArgumentException>(() => MenuRoute.Plan(MenuDestination.Range, ""));
            Assert.Throws<ArgumentException>(() => MenuRoute.BackToMenu(null));
        }
    }
}
