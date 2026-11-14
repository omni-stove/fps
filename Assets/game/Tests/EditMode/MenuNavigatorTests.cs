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
            MenuNavigator.Scenes = MenuScenes.Default;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeNotice();
            CampaignLaunchSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            MenuNavigator.Host = null;
            MenuNavigator.Scenes = null;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeNotice();
            CampaignLaunchSelection.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void GoLoadsTheCampaignSceneSetByTheData()
        {
            const string chapter = "Assets/game/Scenes/Chapter1.unity";
            _host.Scenes.Add(chapter);
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1", null);

            Assert.That(MenuNavigator.Go(MenuDestination.Campaign), Is.True);
            Assert.That(MenuNavigator.Go(MenuDestination.Range), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(chapter), _host.IndexOf(MenuRoute.RangeScene) }),
                "only the overridden scene changes");
        }

        [Test]
        public void OnlineAndReturnLoadTheScenesSetByTheData()
        {
            const string arena = "Assets/game/Scenes/Arena.unity";
            const string title = "Assets/game/Scenes/Title.unity";
            _host.Scenes.Add(arena);
            _host.Scenes.Add(title);
            MenuNavigator.Scenes = new MenuScenes("Title", null, null, "Arena");

            Assert.That(MenuNavigator.Go(MenuDestination.QuickMatch), Is.True);
            Assert.That(MenuNavigator.PendingLobby.Command, Is.EqualTo(LobbyCommand.QuickMatch));
            Assert.That(MenuNavigator.ReturnToMenu(), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(arena), _host.IndexOf(title) }));
        }

        [Test]
        public void ASceneSetByTheDataThatIsNotInTheBuildIsReported()
        {
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1", null);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape("Assets/game/Scenes/Chapter1.unity") + ".*not in the build settings"));

            Assert.That(MenuNavigator.Go(MenuDestination.Campaign), Is.False);

            Assert.That(_host.Loaded, Is.Empty, "the default campaign scene is not loaded instead");
            Assert.That(MenuNavigator.Plan(MenuDestination.Campaign).ScenePath, Is.EqualTo("Assets/game/Scenes/Chapter1.unity"));
        }

        [Test]
        public void WithoutDataTheDefaultScenesAreUsed()
        {
            Assume.That(MenuSceneSettings.Load(), Is.Null, "this project provides its own Resources/" + MenuSceneSettings.ResourcePath);
            MenuNavigator.Scenes = null;

            Assert.That(MenuNavigator.Scenes.ToString(), Is.EqualTo(MenuScenes.Default.ToString()));
            Assert.That(MenuNavigator.Go(MenuDestination.Campaign), Is.True);
            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.CampaignScene) }));
        }

        [TestCase(MenuDestination.Campaign, MenuRoute.CampaignScene)]
        [TestCase(MenuDestination.Range, MenuRoute.RangeScene)]
        [TestCase(MenuDestination.Tutorial, MenuRoute.RangeScene)]
        [TestCase(MenuDestination.CreateLobby, MenuRoute.NetScene)]
        [TestCase(MenuDestination.JoinLobby, MenuRoute.NetScene)]
        [TestCase(MenuDestination.QuickMatch, MenuRoute.NetScene)]
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
                Assert.That(MenuNavigator.ReturnToMenu(DisconnectNotice.ServerStoppedMessage), Is.True);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(calls, Is.EqualTo(new[] { 0 }), "listeners run once, before the menu scene is loaded");
            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.MainMenuScene) }));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a paused game is resumed");
            Assert.That(MenuNavigator.PendingLobby.IsRequested, Is.False);
            Assert.That(MenuNavigator.TakeNotice(), Is.EqualTo(DisconnectNotice.ServerStoppedMessage), "the key is kept for the menu to look up");
            Assert.That(MenuNavigator.TakeNotice().IsEmpty, Is.True, "the notice is shown only once");
        }

        [Test]
        public void ReturnToMenuWithoutANoticeLeavesNone()
        {
            Assert.That(MenuNavigator.ReturnToMenu(LocalizedText.Literal(" ")), Is.True);

            Assert.That(MenuNavigator.PendingNotice.IsEmpty, Is.True);
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
                Assert.That(MenuNavigator.ReturnToMenu(LocalizedText.Literal("bye")), Is.False);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(called, Is.False, "nothing is torn down when the game cannot leave");
            Assert.That(_host.Loaded, Is.Empty);
            Assert.That(MenuNavigator.PendingNotice.IsEmpty, Is.True);
        }

        [Test]
        public void StartCampaignSelectsTheLaunchAndLoadsTheMissionScene()
        {
            using (var missions = new CampaignTestCatalog())
            {
                MenuNavigator.Go(MenuDestination.CreateLobby);
                _host.Loaded.Clear();
                var launch = new CampaignLaunch(CampaignTestCatalog.Mission01, CampaignDifficulty.Easy);
                var order = new List<string>();

                Assert.That(
                    MenuNavigator.StartCampaign(missions.Catalog, launch, () => order.Add("before:" + _host.Loaded.Count)),
                    Is.True);

                Assert.That(order, Is.EqualTo(new[] { "before:0" }), "beforeLoad runs once, before the scene is loaded");
                Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.CampaignScene) }));
                Assert.That(CampaignLaunchSelection.Current.ToString(), Is.EqualTo(launch.ToString()));
                Assert.That(MenuNavigator.PendingLobby.IsRequested, Is.False, "an offline scene clears an unused lobby request");
            }
        }

        [Test]
        public void StartCampaignDoesNothingWhenTheMissionSceneIsNotInTheBuild()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var called = false;
                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(CampaignTestCatalog.NotBuiltScene) + ".*not in the build settings"));

                Assert.That(
                    MenuNavigator.StartCampaign(
                        missions.Catalog,
                        new CampaignLaunch(CampaignTestCatalog.Mission02, CampaignDifficulty.Hard),
                        () => called = true),
                    Is.False);

                Assert.That(called, Is.False, "the save is not deleted for a mission that cannot start");
                Assert.That(_host.Loaded, Is.Empty);
                Assert.That(CampaignLaunchSelection.Current, Is.Null);
            }
        }

        [Test]
        public void StartCampaignLeavesEverythingWhenBeforeLoadFails()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var launch = new CampaignLaunch(CampaignTestCatalog.Mission01, CampaignDifficulty.Normal);

                Assert.Throws<System.IO.IOException>(
                    () => MenuNavigator.StartCampaign(missions.Catalog, launch, () => throw new System.IO.IOException("locked")));

                Assert.That(_host.Loaded, Is.Empty);
                Assert.That(CampaignLaunchSelection.Current, Is.Null);
            }
        }

        [Test]
        public void StartCampaignRejectsAnUnknownMission()
        {
            using (var missions = new CampaignTestCatalog())
            {
                Assert.Throws<System.ArgumentException>(
                    () => MenuNavigator.StartCampaign(missions.Catalog, new CampaignLaunch("Elsewhere", CampaignDifficulty.Normal)));
                Assert.Throws<System.ArgumentNullException>(
                    () => MenuNavigator.StartCampaign(null, new CampaignLaunch("Elsewhere", CampaignDifficulty.Normal)));
                Assert.Throws<System.ArgumentNullException>(() => MenuNavigator.StartCampaign(missions.Catalog, null));
                Assert.That(_host.Loaded, Is.Empty);
            }
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
            public readonly List<string> Scenes = new List<string>
            {
                MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene, MenuRoute.NetScene,
            };

            public readonly HashSet<string> Missing = new HashSet<string>();
            public readonly List<int> Loaded = new List<int>();
            public int QuitCount;

            public int IndexOf(string path)
            {
                return Scenes.IndexOf(path);
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
