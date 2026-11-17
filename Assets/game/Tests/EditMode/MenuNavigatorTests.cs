using System.Collections.Generic;
using System.Linq;
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
            MenuNavigator.SceneRoot = KitPaths.DefaultRoot;
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeReturnReason();
            CampaignLaunchSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            MenuNavigator.Host = null;
            MenuNavigator.Scenes = null;
            MenuNavigator.SceneRoot = null;
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeReturnReason();
            CampaignLaunchSelection.Clear();
            MenuNavigator.OnlineStarter = null;
            Time.timeScale = 1f;
        }

        [Test]
        public void TheOnlineStarterTakesQuickMatchWithoutLoadingAScene()
        {
            var starter = new FakeStarter();
            MenuNavigator.OnlineStarter = starter;

            Assert.That(MenuNavigator.Go(MenuDestination.QuickMatch, out var refusal), Is.True);
            Assert.That(refusal.IsEmpty, Is.True);
            starter.Refusal = MenuReturnReason.OnlineFailed(3, "no lobby");
            LogAssert.Expect(LogType.Log, MenuNavigator.LogTag + " QuickMatch refused (return=OnlineFailed exit=3 detail=\"no lobby\")");
            Assert.That(MenuNavigator.Go(MenuDestination.QuickMatch, out refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(LocalizedText.Literal("no lobby")), "the menu turns the typed refusal into text");

            starter.Refusal = MenuReturnReason.Disconnected(
                DisconnectNotice.Classify(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail), false, "0.1.0"), 5);
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(MenuNavigator.LogTag + " QuickMatch refused (return=Disconnected exit=5 kind=Rejected")));
            Assert.That(MenuNavigator.Go(MenuDestination.QuickMatch, out refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(DisconnectNoticeText.ServerDrainingMessage));
            Assert.That(MenuNavigator.Go(MenuDestination.Range), Is.True, "only the online buttons go to the starter");

            Assert.That(starter.Started, Is.EqualTo(new[] { MenuDestination.QuickMatch, MenuDestination.QuickMatch, MenuDestination.QuickMatch }));
            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.RangeScene) }));
        }

        [Test]
        public void WithoutAnOnlineClientQuickMatchReportsAndStays()
        {
            MenuNavigator.OnlineStarter = null;
            LogAssert.Expect(LogType.Error, new Regex("no online client is installed"));

            Assert.That(MenuNavigator.Go(MenuDestination.QuickMatch, out var refusal), Is.False);

            Assert.That(refusal.IsEmpty, Is.True);
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void GoLoadsTheCampaignSceneSetByTheData()
        {
            const string chapter = "Assets/game/Scenes/Chapter1.unity";
            _host.Scenes.Add(chapter);
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1");

            Assert.That(MenuNavigator.Go(MenuDestination.Campaign), Is.True);
            Assert.That(MenuNavigator.Go(MenuDestination.Range), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(chapter), _host.IndexOf(MenuRoute.RangeScene) }),
                "only the overridden scene changes");
        }

        [Test]
        public void ReturnLoadsTheMenuSetByTheData()
        {
            const string title = "Assets/game/Scenes/Title.unity";
            _host.Scenes.Add(title);
            MenuNavigator.Scenes = new MenuScenes("Title", null, null);

            Assert.That(MenuNavigator.ReturnToMenu(), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(title) }));
        }

        [Test]
        public void ASceneSetByTheDataThatIsNotInTheBuildIsReported()
        {
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1");
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
        public void GoLoadsTheDestinationScene(MenuDestination destination, string scene)
        {
            Assert.That(MenuNavigator.Go(destination), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(scene) }));
            Assert.That(_host.QuitCount, Is.Zero);
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
            _host.Missing.Add(MenuRoute.RangeScene);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuRoute.RangeScene) + ".*not in the build settings"));

            Assert.That(MenuNavigator.Go(MenuDestination.Tutorial), Is.False);

            Assert.That(_host.Loaded, Is.Empty);
            Assert.That(MenuNavigator.PendingTutorial, Is.False, "no request is left behind for a scene that never loads");
        }

        [Test]
        public void ReturnToMenuCleansUpThenLoadsTheMainMenuWithTheNotice()
        {
            MenuNavigator.Go(MenuDestination.Tutorial);
            _host.Loaded.Clear();
            Time.timeScale = 0f;
            var reason = MenuReturnReason.Disconnected(DisconnectNotice.Classify(DisconnectNotice.ServerStoppedReason, true, "0.1.0"), 0);
            var calls = new List<int>();
            void Handler() => calls.Add(_host.Loaded.Count);
            MenuNavigator.ReturningToMenu += Handler;
            try
            {
                Assert.That(MenuNavigator.ReturnToMenu(reason), Is.True);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(calls, Is.EqualTo(new[] { 0 }), "listeners run once, before the menu scene is loaded");
            Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.MainMenuScene) }));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a paused game is resumed");
            Assert.That(MenuNavigator.PendingTutorial, Is.False);
            Assert.That(MenuNavigator.TakeReturnReason(), Is.SameAs(reason), "the typed reason is kept for the menu to turn into text");
            Assert.That(MenuNavigator.TakeReturnReason(), Is.Null, "the reason is shown only once");
        }

        [Test]
        public void ReturnToMenuWithoutAReasonLeavesNone()
        {
            Assert.That(MenuNavigator.ReturnToMenu(MenuReturnReason.OnlineFailed(3, "stale")), Is.True);
            Assert.That(MenuNavigator.ReturnToMenu(), Is.True);

            Assert.That(MenuNavigator.PendingReturnReason, Is.Null, "returning without a reason drops an earlier one");
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
            Assert.That(_host.Loaded.Count(), Is.EqualTo(1));
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
                Assert.That(MenuNavigator.ReturnToMenu(MenuReturnReason.OnlineFailed(3, "bye")), Is.False);
            }
            finally
            {
                MenuNavigator.ReturningToMenu -= Handler;
            }

            Assert.That(called, Is.False, "nothing is torn down when the game cannot leave");
            Assert.That(_host.Loaded, Is.Empty);
            Assert.That(MenuNavigator.PendingReturnReason, Is.Null);
        }

        [Test]
        public void StartCampaignSelectsTheLaunchAndLoadsTheMissionScene()
        {
            using (var missions = new CampaignTestCatalog())
            {
                MenuNavigator.Go(MenuDestination.Tutorial);
                _host.Loaded.Clear();
                var launch = new CampaignLaunch(CampaignTestCatalog.Mission01, CampaignDifficulty.Easy);
                var order = new List<string>();

                Assert.That(
                    MenuNavigator.StartCampaign(missions.Catalog, launch, () => order.Add("before:" + _host.Loaded.Count)),
                    Is.True);

                Assert.That(order, Is.EqualTo(new[] { "before:0" }), "beforeLoad runs once, before the scene is loaded");
                Assert.That(_host.Loaded, Is.EqualTo(new[] { _host.IndexOf(MenuRoute.CampaignScene) }));
                Assert.That(CampaignLaunchSelection.Current.ToString(), Is.EqualTo(launch.ToString()));
                Assert.That(MenuNavigator.PendingTutorial, Is.False, "the campaign clears an unused tutorial request");
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
                MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene,
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

        private sealed class FakeStarter : IOnlineMatchStarter
        {
            public List<MenuDestination> Started { get; } = new List<MenuDestination>();

            public MenuReturnReason Refusal { get; set; }

            public bool Start(MenuDestination destination, out MenuReturnReason refusal)
            {
                Started.Add(destination);
                refusal = Refusal;
                return Refusal == null;
            }
        }
    }
}
