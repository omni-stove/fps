using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class ServerBrowserPanelTests
    {
        private VisualElement _container;
        private ServerBrowserPanel _panel;
        private FakeServerBrowserControl _control;
        private int _cancelled;

        private static readonly ServerListing _harbor =
            new ServerListing("one", "203.0.113.1:7777", "europe", "tdm", "harbor", 3, 8, 80);

        private static readonly ServerListing _quarry =
            new ServerListing("two", "203.0.113.2:7777", "asia", "tdm", "quarry", 5, 8, 20);

        private static readonly ServerListing _full =
            new ServerListing("full", "203.0.113.3:7777", "europe", "tdm", "quarry", 8, 8, 40);

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _container = new VisualElement();
            _control = new FakeServerBrowserControl();
            _cancelled = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
        }

        private void Open()
        {
            _panel = new ServerBrowserPanel(_container);
            _panel.Cancelled += () => _cancelled++;
        }

        private static void Publish(ServerBrowserStage stage, params ServerListing[] servers)
        {
            GameSignals.ServerBrowser.Publish(new ServerBrowserStatus(stage, servers));
        }

        private static string Text(LocalizedText text)
        {
            return Localizer.Resolve(text);
        }

        [Test]
        public void OpeningAsksForTheListAndShowsItByLowestPing()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();

            Assert.That(_control.Refreshes, Is.EqualTo(1));
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.LoadingKey))));

            Publish(ServerBrowserStage.Ready, _harbor, _quarry, _full);

            Assert.That(_panel.Notice, Is.Null);
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "two", "full", "one" }));
            Assert.That(_panel.RowTexts[0], Is.EqualTo(string.Join(" ",
                Text(ServerBrowserText.Region("asia")),
                Text(ServerBrowserText.Mode("tdm")),
                Text(ServerBrowserText.Map("quarry")),
                Text(ServerBrowserText.Players(_quarry)),
                Text(ServerBrowserText.Ping(_quarry)))));
            Assert.That(_panel.RowButtons[0].enabledSelf, Is.True);
            Assert.That(_panel.RowButtons[1].enabledSelf, Is.False, "a full server cannot be picked");
            Assert.That(_panel.RowButtons[1].ClassListContains(ServerBrowserPanel.FullRowClass), Is.True);
        }

        [Test]
        public void TheFiltersCycleThroughTheListedValuesAndBackToAny()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _harbor, _quarry, _full);

            _panel.CycleRegion();
            Assert.That(_panel.Query.Region, Is.EqualTo("asia"));
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "two" }));
            Assert.That(_panel.RegionFilterButton.text, Is.EqualTo(Text(ServerBrowserText.RegionFilter("asia"))));
            Assert.That(_panel.RegionFilterButton.ClassListContains(ServerBrowserPanel.ActiveFilterClass), Is.True);

            _panel.CycleRegion();
            Assert.That(_panel.Query.Region, Is.EqualTo("europe"));
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "full", "one" }));

            _panel.CycleRegion();
            Assert.That(_panel.Query.Region, Is.Null);
            Assert.That(_panel.RowIds, Has.Count.EqualTo(3));

            _panel.CycleMap();
            Assert.That(_panel.Query.Map, Is.EqualTo("harbor"));
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "one" }));
        }

        [Test]
        public void HidingFullServersAndSortingByPlayers()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _harbor, _quarry, _full);

            _panel.ToggleHideFull();
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "two", "one" }));
            Assert.That(_panel.FullFilterButton.text, Is.EqualTo(Text(ServerBrowserText.FullFilter(true))));

            _panel.ToggleHideFull();
            _panel.SortBy(ServerListSort.Players);
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "full", "two", "one" }));
            Assert.That(_panel.ColumnButton(ServerListSort.Players).ClassListContains(ServerBrowserPanel.SortedColumnClass), Is.True);
            Assert.That(_panel.ColumnButton(ServerListSort.Ping).ClassListContains(ServerBrowserPanel.SortedColumnClass), Is.False);

            _panel.SortBy(ServerListSort.Players);
            Assert.That(_panel.RowIds, Is.EqualTo(new[] { "one", "two", "full" }));
            Assert.That(_panel.ColumnButton(ServerListSort.Players).text,
                Is.EqualTo(Text(ServerBrowserText.Column(ServerListSort.Players, _panel.Query))));
        }

        [Test]
        public void FiltersThatLeaveNothingSaySo()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _full);

            _panel.ToggleHideFull();

            Assert.That(_panel.RowIds, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.NoMatchKey))));
        }

        [Test]
        public void PickingAServerJoinsItAndBackDoesNothingOnceJoined()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _harbor, _quarry);

            Assert.That(_panel.Join("one"), Is.True);

            Assert.That(_control.Joins, Is.EqualTo(new[] { "one" }));
            Assert.That(_panel.IsJoining, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.JoiningKey))));
            Assert.That(_panel.RowButtons.All(row => !row.enabledSelf), Is.True, "no second pick while joining");
            Assert.That(_panel.Join("two"), Is.False);

            _control.Answer(ServerJoinOutcome.Joined);

            Assert.That(_panel.HasJoined, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.JoinedKey))));
            _panel.Cancel();
            Assert.That(_cancelled, Is.Zero, "the joined match is loading");

            _panel.Dispose();
            _panel = null;
            Assert.That(_control.Closes, Is.Zero, "the joined match keeps its connection");
        }

        [TestCase(ServerJoinOutcome.Full, ServerBrowserText.FullKey)]
        [TestCase(ServerJoinOutcome.NotFound, ServerBrowserText.NotFoundKey)]
        [TestCase(ServerJoinOutcome.Failed, ServerBrowserText.FailedKey)]
        public void AServerThatCannotBeJoinedSaysWhyAndCanBePickedAgain(ServerJoinOutcome outcome, string key)
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _harbor);
            _panel.Join("one");

            _control.Answer(outcome);

            Assert.That(_panel.IsJoining, Is.False);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(key))));
            Assert.That(_panel.IsNoticeError, Is.True);
            Assert.That(_panel.RowButtons[0].enabledSelf, Is.True);

            Assert.That(_panel.Refresh(), Is.True);
            Assert.That(_control.Refreshes, Is.EqualTo(2));
            Assert.That(_panel.IsNoticeError, Is.False, "refreshing clears the join notice");
        }

        [Test]
        public void WithoutTheServiceItSaysTheListIsUnavailable()
        {
            Open();

            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.UnavailableKey))));
            Assert.That(_panel.RefreshButton.enabledSelf, Is.False);
            Assert.That(_panel.Refresh(), Is.False);
            Assert.That(_panel.Join("one"), Is.False);

            GameSignals.ServerBrowserControl.Publish(_control);

            Assert.That(_control.Refreshes, Is.EqualTo(1), "a service published later is opened");
            Assert.That(_panel.RefreshButton.enabledSelf, Is.True);
        }

        [Test]
        public void AnUnavailableListShowsNoRows()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();
            Publish(ServerBrowserStage.Ready, _harbor);

            Publish(ServerBrowserStage.Unavailable);

            Assert.That(_panel.RowIds, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(ServerBrowserText.UnavailableKey))));
        }

        [Test]
        public void ClosingThePanelClosesTheList()
        {
            GameSignals.ServerBrowserControl.Publish(_control);
            Open();

            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));

            _panel.Dispose();
            _panel = null;

            Assert.That(_control.Closes, Is.EqualTo(1));
            Assert.That(_container.childCount, Is.Zero);
        }

        [Test]
        public void NextStepsThroughTheValuesAndStartsOverFromAValueNoLongerListed()
        {
            var values = new[] { "asia", "europe" };

            Assert.That(ServerBrowserPanel.Next(null, values), Is.EqualTo("asia"));
            Assert.That(ServerBrowserPanel.Next("asia", values), Is.EqualTo("europe"));
            Assert.That(ServerBrowserPanel.Next("europe", values), Is.Null);
            Assert.That(ServerBrowserPanel.Next("africa", values), Is.EqualTo("asia"));
            Assert.That(ServerBrowserPanel.Next("asia", Array.Empty<string>()), Is.Null);
            Assert.Throws<ArgumentNullException>(() => ServerBrowserPanel.Next(null, null));
        }

        [Test]
        public void TheTextsResolveInEveryLanguage()
        {
            Assert.That(Text(ServerBrowserText.Mode("tdm")), Is.Not.EqualTo("tdm"), "the kit's mode has a name");
            Assert.That(Text(ServerBrowserText.Mode("custom-mode")), Is.EqualTo("custom-mode"), "an unnamed mode shows its key");
            Assert.That(Text(ServerBrowserText.Region(null)), Is.EqualTo(Text(new LocalizedText(ServerBrowserText.UnknownKey))));
            Assert.That(Text(ServerBrowserText.Map("NoSuchMap")), Is.EqualTo("NoSuchMap"));
            Assert.That(Text(ServerBrowserText.Ping(_harbor.WithPing(null))), Is.EqualTo(Text(new LocalizedText(ServerBrowserText.UnknownKey))));
        }

        private sealed class FakeServerBrowserControl : IServerBrowserControl
        {
            private Action<ServerJoinOutcome> _pending;

            public int Refreshes { get; private set; }

            public int Closes { get; private set; }

            public List<string> Joins { get; } = new List<string>();

            public bool Refresh()
            {
                Refreshes++;
                return true;
            }

            public void Join(string serverId, Action<ServerJoinOutcome> done)
            {
                Joins.Add(serverId);
                _pending = done;
            }

            public void Close()
            {
                Closes++;
            }

            public void Answer(ServerJoinOutcome outcome)
            {
                var done = _pending;
                _pending = null;
                done(outcome);
            }
        }
    }
}
