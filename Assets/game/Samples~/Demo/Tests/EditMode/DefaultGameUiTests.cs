using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class DefaultGameUiTests
    {
        [TearDown]
        public void TearDown()
        {
            GameUi.Pause = null;
            GameUi.LoadoutSelect = null;
            GameUi.Menu = null;
        }

        [Test]
        public void Defaults_AreRegisteredByTheUi()
        {
            Assert.That(GameUi.Pause, Is.Not.Null);
            Assert.That(GameUi.LoadoutSelect, Is.Not.Null);
            Assert.That(GameUi.Menu, Is.Not.Null);
            Assert.That(GameUi.IsPauseOpen, Is.EqualTo(PauseMenu.IsAnyOpen));
            Assert.That(GameUi.IsLoadoutSelectOpen, Is.EqualTo(LoadoutSelectView.IsAnyOpen));
        }

        [Test]
        public void DefaultMenu_TakesTheMenuNavigatorRequests()
        {
            var pendingTutorial = MenuNavigator.PendingTutorial;

            Assert.That(GameUi.Menu.TakeTutorialRequest(), Is.EqualTo(pendingTutorial));
            Assert.That(MenuNavigator.PendingTutorial, Is.False);
        }
    }
}
