using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    public sealed class MenuNavigatorTests
    {
        private FakeHost _host;

        [SetUp]
        public void SetUp()
        {
            _host = new FakeHost();
            MenuNavigator.Host = _host;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeNotice();
        }

        [TearDown]
        public void TearDown()
        {
            MenuNavigator.Host = null;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeNotice();
            Time.timeScale = 1f;
        }

        [TestCase(MenuDestination.Campaign, MenuRoute.CampaignScene)]
        [TestCase(MenuDestination.Range, MenuRoute.RangeScene)]
        [TestCase(MenuDestination.Tutorial, MenuRoute.RangeScene)]
        [TestCase(MenuDestination.CreateLobby, MenuRoute.NetScene)]
        [TestCase(MenuDestination.JoinLobby, MenuRoute.NetScene)]
        public void GoLoadsTheDestinationScene(MenuDestination destination, string scene)
        {
            Assert.That(MenuNavigator.Go(destination), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(scene) }));
            Assert.That(_host.QuitCount, Is.Zero);
        }

        [Test]
        public void OnlineLeavesTheLobbyRequestForTheNetScene()
        {
            MenuNavigator.Go(MenuDestination.CreateLobby);

            var created = MenuNavigator.TakeLobbyRequest();
            Assert.That(created.Command, Is.EqualTo(LobbyCommand.Create));
            Assert.That(MenuNavigator.TakeLobbyRequest().IsRequested, Is.False, "the request is handed over only once");

            MenuNavigator.Go(MenuDestination.JoinLobby);
            Assert.That(MenuNavigator.PendingLobby.Command, Is.EqualTo(LobbyCommand.Join));

            MenuNavigator.Go(MenuDestination.Range);
            Assert.That(MenuNavigator.PendingLobby.IsRequested, Is.False, "an offline scene clears an unused request");
        }

        [Test]
        public void TutorialLeavesTheRequestForTheRangeOnce()
        {
            MenuNavigator.Go(MenuDestination.Tutorial);

            Assert.That(MenuNavigator.PendingTutorial, Is.True);
            Assert.That(MenuNavigator.TakeTutorialRequest(), Is.True);
            Assert.That(MenuNavigator.TakeTutorialRequest(), Is.False, "the request is handed over only once");

            MenuNavigator.Go(MenuDestination.Tutorial);
            MenuNavigator.Go(MenuDestination.Range);
            Assert.That(MenuNavigator.PendingTutorial, Is.False, "the plain range clears an unused request");
        }

        [Test]
        public void TutorialLeavesNoRequestWhenTheRangeIsNotInTheBuild()
        {
            _host.Missing.Add(MenuRoute.RangeScene);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuRoute.RangeScene) + ".*not in the build settings"));

            Assert.That(MenuNavigator.Go(MenuDestination.Tutorial), Is.False);

            Assert.That(MenuNavigator.PendingTutorial, Is.False);
        }

        [Test]
        public void ReturnToMenuDropsAnUnusedTutorialRequest()
        {
            MenuNavigator.Go(MenuDestination.Tutorial);

            Assert.That(MenuNavigator.ReturnToMenu(), Is.True);

            Assert.That(MenuNavigator.PendingTutorial, Is.False);
        }

        [Test]
        public void QuitQuitsWithoutLoading()
        {
            Assert.That(MenuNavigator.Go(MenuDestination.Quit), Is.True);

            Assert.That(_host.QuitCount, Is.EqualTo(1));
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void GoFailsAndKeepsStateWhenTheSceneIsNotInTheBuild()
        {
            _host.Missing.Add(MenuRoute.NetScene);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuRoute.NetScene) + ".*not in the build settings"));

            Assert.That(MenuNavigator.Go(MenuDestination.CreateLobby), Is.False);

            Assert.That(_host.Loaded, Is.Empty);
            Assert.That(MenuNavigator.PendingLobby.IsRequested, Is.False, "no request is left behind for a scene that never loads");
        }

        [Test]
        public void ReturnToMenuCleansUpThenLoadsTheMainMenuWithTheNotice()
        {
            MenuNavigator.Go(MenuDestination.JoinLobby);
            _host.Loaded.Clear();
            Time.timeScale = 0f;
            var calls = new List<int>();
            void Handler() => calls.Add(_host.Loaded.Count);
            MenuNavigator.ReturningToMenu += Handler;
            try
            {
                Assert.That(MenuNavigator.ReturnToMenu("Disconnected from the host."), Is.True);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(calls, Is.EqualTo(new[] { 0 }), "listeners run once, before the menu scene is loaded");
            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.MainMenuScene) }));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a paused game is resumed");
            Assert.That(MenuNavigator.PendingLobby.IsRequested, Is.False);
            Assert.That(MenuNavigator.TakeNotice(), Is.EqualTo("Disconnected from the host."));
            Assert.That(MenuNavigator.TakeNotice(), Is.Null, "the notice is shown only once");
        }

        [Test]
        public void ReturnToMenuWithoutANoticeLeavesNone()
        {
            Assert.That(MenuNavigator.ReturnToMenu(" "), Is.True);

            Assert.That(MenuNavigator.PendingNotice, Is.Null);
        }

        [Test]
        public void AFailingListenerDoesNotStopTheReturn()
        {
            var after = 0;
            void Throwing() => throw new System.InvalidOperationException("boom");
            void Counting() => after++;
            MenuNavigator.ReturningToMenu += Throwing;
            MenuNavigator.ReturningToMenu += Counting;
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            try
            {
                Assert.That(MenuNavigator.ReturnToMenu(), Is.True);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Throwing;
                MenuNavigator.ReturningToMenu -= Counting;
            }

            Assert.That(after, Is.EqualTo(1));
            Assert.That(_host.Loaded, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReturnToMenuStaysWhenTheMenuIsNotInTheBuild()
        {
            _host.Missing.Add(MenuRoute.MainMenuScene);
            var called = false;
            void Handler() => called = true;
            MenuNavigator.ReturningToMenu += Handler;
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuRoute.MainMenuScene) + ".*not in the build settings"));
            try
            {
                Assert.That(MenuNavigator.ReturnToMenu("bye"), Is.False);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(called, Is.False, "nothing is torn down when the game cannot leave");
            Assert.That(_host.Loaded, Is.Empty);
            Assert.That(MenuNavigator.PendingNotice, Is.Null);
        }

        [Test]
        public void HostFallsBackToTheSceneManager()
        {
            MenuNavigator.Host = null;

            Assert.That(MenuNavigator.Host, Is.Not.Null);
            Assert.That(MenuNavigator.Host, Is.Not.SameAs(_host));
        }

        private sealed class FakeHost : IMenuSceneHost
        {
            private static readonly string[] _scenes =
            {
                MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene, MenuRoute.NetScene,
            };

            public readonly HashSet<string> Missing = new HashSet<string>();
            public readonly List<int> Loaded = new List<int>();
            public int QuitCount;

            public int IndexOf(string path)
            {
                return System.Array.IndexOf(_scenes, path);
            }

            public int BuildIndexOf(string path)
            {
                return Missing.Contains(path) ? -1 : IndexOf(path);
            }

            public void Load(int buildIndex)
            {
                Loaded.Add(buildIndex);
            }

            public void Quit()
            {
                QuitCount++;
            }
        }
    }
}
