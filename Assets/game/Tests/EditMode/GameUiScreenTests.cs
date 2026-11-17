using System;
using System.Collections.Generic;
using System.Reflection;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    public sealed class GameUiScreenTests
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
        public void GameUi_OnlyCarriesTheWaysFromTheUiIntoTheGame()
        {
            var names = new List<string>();
            foreach (var member in typeof(GameUi).GetMembers(BindingFlags.Public | BindingFlags.Static))
            {
                names.Add(member.Name);
            }

            Assert.That(names, Has.None.Contains("Hud"), "the game no longer builds or finds the HUD through GameUi");
            Assert.That(names, Has.None.EqualTo("AttachActionDebug"));
            Assert.That(typeof(GameUi).Assembly.GetType("Fps.Runtime.IHud"), Is.Null);
            Assert.That(typeof(GameUi).Assembly.GetType("Fps.Runtime.IHudFactory"), Is.Null);
            Assert.That(typeof(GameUi).Assembly.GetType("Fps.Runtime.IModeHud"), Is.Null);
        }

        [Test]
        public void Pause_Replacement_IsUsedUntilCleared()
        {
            var defaultPause = GameUi.Pause;
            GameUi.Pause = new FakePause { IsAnyOpen = true };

            Assert.That(GameUi.IsPauseOpen, Is.True);

            GameUi.Pause = null;
            Assert.That(GameUi.Pause, Is.SameAs(defaultPause), "clearing the replacement goes back to the default");
        }

        [Test]
        public void LoadoutSelect_Replacement_IsUsedUntilCleared()
        {
            var defaultLoadoutSelect = GameUi.LoadoutSelect;
            GameUi.LoadoutSelect = new FakeLoadoutSelect { IsAnyOpen = true };

            Assert.That(GameUi.IsLoadoutSelectOpen, Is.True);

            GameUi.LoadoutSelect = null;
            Assert.That(GameUi.LoadoutSelect, Is.SameAs(defaultLoadoutSelect));
        }

        [Test]
        public void UseDefaults_RejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameUi.UseDefaultPause(null));
            Assert.Throws<ArgumentNullException>(() => GameUi.UseDefaultLoadoutSelect(null));
            Assert.Throws<ArgumentNullException>(() => GameUi.UseDefaultMenu(null));
        }

        [Test]
        public void UseDefault_DoesNotOverrideAReplacement()
        {
            var defaultMenu = GameUi.Menu;
            var replacement = new FakeMenu();
            GameUi.Menu = replacement;
            try
            {
                GameUi.UseDefaultMenu(new FakeMenu());
                Assert.That(GameUi.Menu, Is.SameAs(replacement));
            }
            finally
            {
                GameUi.UseDefaultMenu(defaultMenu);
            }
        }

        [Test]
        public void DefaultMenu_TakesTheMenuNavigatorRequests()
        {
            var pendingTutorial = MenuNavigator.PendingTutorial;

            Assert.That(GameUi.Menu.TakeTutorialRequest(), Is.EqualTo(pendingTutorial));
            Assert.That(MenuNavigator.PendingTutorial, Is.False);
        }

        [Test]
        public void AppliedSink_ReadsTheCurrentSettings()
        {
            Assert.That(AppliedControlSettings.Sink.Current, Is.SameAs(AppliedControlSettings.Current));
            Assert.Throws<ArgumentNullException>(() => AppliedControlSettings.Sink.Apply(null));
        }

        private sealed class FakePause : IPauseState
        {
            public bool IsAnyOpen { get; set; }
        }

        private sealed class FakeLoadoutSelect : ILoadoutSelect
        {
            public event Action<IReadOnlyList<string>> Confirmed
            {
                add { }
                remove { }
            }

            public bool IsAnyOpen { get; set; }

            public bool IsOpen => false;

            public void Open(LoadoutSelection selection)
            {
            }

            public void Close()
            {
            }

            public void ShowSending()
            {
            }

            public void ShowAnswer(bool accepted, string reason)
            {
            }
        }

        private sealed class FakeMenu : IMenuNavigator
        {
            public event Action ReturningToMenu
            {
                add { }
                remove { }
            }

            public bool TakeTutorialRequest()
            {
                return false;
            }

            public bool ReturnToMenu(MenuReturnReason reason)
            {
                return false;
            }
        }
    }
}
