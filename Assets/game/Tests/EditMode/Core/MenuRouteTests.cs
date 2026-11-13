using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MenuRouteTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        [Test]
        public void CampaignLoadsTheFirstCampaignSceneWithoutALobby()
        {
            var transition = MenuRoute.Plan(MenuDestination.Campaign);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.CampaignScene));
            Assert.That(transition.Lobby.IsRequested, Is.False);
        }

        [Test]
        public void RangeLoadsTheBootstrapSceneWithoutALobby()
        {
            var transition = MenuRoute.Plan(MenuDestination.Range);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(transition.Lobby.IsRequested, Is.False);
            Assert.That(transition.StartsTutorial, Is.False, "the range shows the tutorial only until it has been finished");
        }

        [Test]
        public void TutorialLoadsTheRangeAndAsksForTheTutorial()
        {
            var transition = MenuRoute.Plan(MenuDestination.Tutorial);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(transition.Lobby.IsRequested, Is.False);
            Assert.That(transition.StartsTutorial, Is.True);
            Assert.That(transition.ToString(), Does.EndWith(" tutorial"));
        }

        [Test]
        public void OnlyTheTutorialAsksForTheTutorial()
        {
            foreach (var destination in MenuRoute.Destinations)
            {
                Assert.That(MenuRoute.Plan(destination).StartsTutorial, Is.EqualTo(destination == MenuDestination.Tutorial), destination.ToString());
            }

            Assert.That(MenuRoute.BackToMenu().StartsTutorial, Is.False);
        }

        [Test]
        public void CreateLobbyLoadsTheNetSceneLikeLobbyCreate()
        {
            var transition = MenuRoute.Plan(MenuDestination.CreateLobby);
            Assert.That(LobbyLaunchOptions.TryParse(Args("-lobby create"), out var fromArgs, out var error), Is.True, error);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.NetScene));
            Assert.That(transition.Lobby.Command, Is.EqualTo(LobbyCommand.Create));
            Assert.That(transition.Lobby.MaxPlayers, Is.EqualTo(fromArgs.MaxPlayers));
            Assert.That(transition.Lobby.LobbyId, Is.Null);
        }

        [Test]
        public void JoinLobbyLoadsTheNetSceneLikeLobbyJoin()
        {
            var transition = MenuRoute.Plan(MenuDestination.JoinLobby);
            Assert.That(LobbyLaunchOptions.TryParse(Args("-lobby join"), out var fromArgs, out var error), Is.True, error);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.NetScene));
            Assert.That(transition.Lobby.Command, Is.EqualTo(LobbyCommand.Join));
            Assert.That(transition.Lobby.LobbyId, Is.EqualTo(fromArgs.LobbyId), "searches for a lobby instead of naming one");
        }

        [Test]
        public void QuitLoadsNothing()
        {
            var transition = MenuRoute.Plan(MenuDestination.Quit);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.Quit));
            Assert.That(transition.ScenePath, Is.Null);
            Assert.That(transition.Lobby.IsRequested, Is.False);
        }

        [Test]
        public void EveryListedDestinationHasAPlanAndTheListHasNoDuplicates()
        {
            Assert.That(MenuRoute.Destinations, Is.Unique);
            // Unity の .NET には Enum.GetValues<TEnum>() が無いので、dotnet でだけ出る CA2263 を止める
#pragma warning disable CA2263
            foreach (MenuDestination destination in Enum.GetValues(typeof(MenuDestination)))
#pragma warning restore CA2263
            {
                Assert.That(MenuRoute.Destinations, Has.Member(destination));
                Assert.That(MenuRoute.Plan(destination), Is.Not.Null);
            }
        }

        [Test]
        public void RejectsAnUnknownDestination()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MenuRoute.Plan((MenuDestination)99));
        }

        [Test]
        public void BackToMenuLoadsTheMainMenuWithoutALobby()
        {
            var transition = MenuRoute.BackToMenu();

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.MainMenuScene));
            Assert.That(transition.Lobby.IsRequested, Is.False);
        }

        [Test]
        public void ScenesAreDistinct()
        {
            Assert.That(
                new[] { MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene, MenuRoute.NetScene },
                Is.Unique);
        }

        [Test]
        public void TransitionRejectsAnEmptyScene()
        {
            Assert.Throws<ArgumentException>(() => MenuTransition.Load(null));
            Assert.Throws<ArgumentException>(() => MenuTransition.Load(" "));
            Assert.That(MenuTransition.Load(MenuRoute.RangeScene, null).Lobby, Is.SameAs(LobbyLaunchOptions.None));
            Assert.That(MenuTransition.Load(MenuRoute.RangeScene).StartsTutorial, Is.False);
            Assert.That(MenuTransition.Quit.StartsTutorial, Is.False);
        }

        [TestCase("Fps.app")]
        [TestCase("Fps.app -difficulty hard")]
        [TestCase("Fps.app -batchmode")]
        public void ShowsTheMenuWithoutNetArguments(string commandLine)
        {
            Assert.That(MenuRoute.ShouldSkipMenu(false, false, Args(commandLine)), Is.False);
        }

        [TestCase("Fps.app -lobby create")]
        [TestCase("Fps.app -lobby join -lobby-id abc")]
        [TestCase("Fps.app -mode client -address 127.0.0.1")]
        [TestCase("Fps.app -mode host")]
        public void SkipsTheMenuWithLobbyOrModeArguments(string commandLine)
        {
            Assert.That(MenuRoute.ShouldSkipMenu(false, false, Args(commandLine)), Is.True);
        }

        [Test]
        public void SkipsTheMenuOnADedicatedServerButNeverInTheEditor()
        {
            Assert.That(MenuRoute.ShouldSkipMenu(false, true, Args("Server")), Is.True);
            Assert.That(MenuRoute.ShouldSkipMenu(true, false, Args("Unity -lobby create")), Is.False);
        }

        [TestCase(0, 1, 6, 1)]
        [TestCase(4, 1, 6, 5)]
        [TestCase(5, 1, 6, 0)]
        [TestCase(0, -1, 6, 5)]
        [TestCase(3, -1, 6, 2)]
        [TestCase(2, 0, 6, 2)]
        [TestCase(1, 7, 6, 2)]
        [TestCase(1, -8, 6, 5)]
        public void MoveFocusWrapsAroundTheEnds(int current, int step, int count, int expected)
        {
            Assert.That(MenuRoute.MoveFocus(current, step, count), Is.EqualTo(expected));
        }

        [TestCase(-1, 1, 6, 0)]
        [TestCase(-1, 0, 6, 0)]
        [TestCase(-1, -1, 6, 5)]
        [TestCase(9, 1, 6, 0)]
        [TestCase(0, 1, 0, -1)]
        public void MoveFocusWithoutAFocusStartsAtAnEnd(int current, int step, int count, int expected)
        {
            Assert.That(MenuRoute.MoveFocus(current, step, count), Is.EqualTo(expected));
        }

        [Test]
        public void MoveFocusRejectsANegativeCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MenuRoute.MoveFocus(0, 1, -1));
        }

        [Test]
        public void TheMenuLobbyIsUsedOnlyWithoutLobbyOrModeArguments()
        {
            var menu = LobbyLaunchOptions.Join();
            var fromArgs = LobbyLaunchOptions.Create();

            Assert.That(NetStartup.ChooseLobby(LobbyLaunchOptions.None, Args("Fps.app"), false, menu), Is.SameAs(menu));
            Assert.That(NetStartup.ChooseLobby(fromArgs, Args("Fps.app -lobby create"), false, menu), Is.SameAs(fromArgs));
            Assert.That(
                NetStartup.ChooseLobby(LobbyLaunchOptions.None, Args("Fps.app -mode host"), false, menu),
                Is.SameAs(LobbyLaunchOptions.None),
                "-mode connects directly, so a stale menu request is dropped");
            Assert.That(
                NetStartup.ChooseLobby(LobbyLaunchOptions.None, Args("Server"), true, menu),
                Is.SameAs(LobbyLaunchOptions.None),
                "a dedicated server cannot own a lobby");
        }

        [Test]
        public void WithoutAMenuLobbyTheArgumentsDecide()
        {
            Assert.That(NetStartup.ChooseLobby(LobbyLaunchOptions.None, Args("Fps.app"), false, null), Is.SameAs(LobbyLaunchOptions.None));
            Assert.That(
                NetStartup.ChooseLobby(LobbyLaunchOptions.None, Args("Fps.app"), false, LobbyLaunchOptions.None),
                Is.SameAs(LobbyLaunchOptions.None));
            Assert.Throws<ArgumentNullException>(() => NetStartup.ChooseLobby(null, Args("Fps.app"), false, null));
            Assert.Throws<ArgumentNullException>(() => NetStartup.ChooseLobby(LobbyLaunchOptions.None, null, false, null));
        }
    }
}
