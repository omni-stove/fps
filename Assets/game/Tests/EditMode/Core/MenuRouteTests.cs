using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MenuRouteTests
    {
        [Test]
        public void CampaignLoadsTheFirstCampaignScene()
        {
            var transition = MenuRoute.Plan(MenuDestination.Campaign);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.CampaignScene));
        }

        [Test]
        public void RangeLoadsTheBootstrapScene()
        {
            var transition = MenuRoute.Plan(MenuDestination.Range);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(transition.StartsTutorial, Is.False, "the range shows the tutorial only until it has been finished");
        }

        [Test]
        public void TutorialLoadsTheRangeAndAsksForTheTutorial()
        {
            var transition = MenuRoute.Plan(MenuDestination.Tutorial);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.RangeScene));
            Assert.That(transition.StartsTutorial, Is.True);
            Assert.That(transition.ToString(), Does.EndWith(" tutorial"));
        }

        [Test]
        public void OnlyTheTutorialAsksForTheTutorial()
        {
            foreach (var destination in MenuRoute.Destinations)
            {
                if (destination != MenuDestination.QuickMatch)
                {
                    Assert.That(MenuRoute.Plan(destination).StartsTutorial, Is.EqualTo(destination == MenuDestination.Tutorial), destination.ToString());
                }
            }

            Assert.That(MenuRoute.BackToMenu().StartsTutorial, Is.False);
        }

        [Test]
        public void QuickMatchIsStartedByTheOnlineClientNotByAScene()
        {
            Assert.Throws<ArgumentException>(() => MenuRoute.Plan(MenuDestination.QuickMatch));
            Assert.That(MenuRoute.Destinations, Has.Member(MenuDestination.QuickMatch));
        }

        [Test]
        public void QuitLoadsNothing()
        {
            var transition = MenuRoute.Plan(MenuDestination.Quit);

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.Quit));
            Assert.That(transition.ScenePath, Is.Null);
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
                if (destination != MenuDestination.QuickMatch)
                {
                    Assert.That(MenuRoute.Plan(destination), Is.Not.Null);
                }
            }
        }

        [Test]
        public void RejectsAnUnknownDestination()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MenuRoute.Plan((MenuDestination)99));
        }

        [Test]
        public void BackToMenuLoadsTheMainMenu()
        {
            var transition = MenuRoute.BackToMenu();

            Assert.That(transition.Kind, Is.EqualTo(MenuTransitionKind.LoadScene));
            Assert.That(transition.ScenePath, Is.EqualTo(MenuRoute.MainMenuScene));
        }

        [Test]
        public void ScenesAreDistinct()
        {
            Assert.That(
                new[] { MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene },
                Is.Unique);
        }

        [Test]
        public void TransitionRejectsAnEmptyScene()
        {
            Assert.Throws<ArgumentException>(() => MenuTransition.Load(null));
            Assert.Throws<ArgumentException>(() => MenuTransition.Load(" "));
            Assert.That(MenuTransition.Load(MenuRoute.RangeScene).StartsTutorial, Is.False);
            Assert.That(MenuTransition.Quit.StartsTutorial, Is.False);
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
    }
}
