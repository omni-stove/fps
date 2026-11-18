using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class LeaderboardPanelTests
    {
        private VisualElement _container;
        private LeaderboardPanel _panel;
        private FakeLeaderboardControl _control;
        private int _cancelled;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _control = new FakeLeaderboardControl();
            _cancelled = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
        }

        private void Open(LeaderboardSpec spec = null)
        {
            _panel = new LeaderboardPanel(_container, spec ?? LeaderboardSpec.Default);
            _panel.Cancelled += () => _cancelled++;
        }

        private static string Text(LocalizedText text)
        {
            return Localizer.Resolve(text);
        }

        [Test]
        public void OpensTheFirstBoardAndListsTheRanksItIsSent()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open();

            Assert.That(_control.Opened, Is.EqualTo(new[] { "kills" }));
            Assert.That(_panel.SelectedBoardId, Is.EqualTo("kills"));
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.LoadingKey))));
            Assert.That(_panel.RowTexts, Is.Empty);

            var ace = new LeaderboardRow(1, "Ace", 9000000000L, false);
            var me = new LeaderboardRow(57, "Me", 12, true);
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { ace, me }));

            Assert.That(_panel.Notice, Is.Null);
            Assert.That(_panel.RowTexts, Is.EqualTo(new[]
            {
                Text(LeaderboardText.Rank(ace)) + " Ace 9,000,000,000",
                Text(LeaderboardText.Rank(me)) + " " + Text(new LocalizedText(LeaderboardText.YouKey, LocalizedText.Literal("Me"))) + " 12",
            }));
            Assert.That(_panel.RowElements[0].ClassListContains(LeaderboardPanel.LocalRowClass), Is.False);
            Assert.That(_panel.RowElements[1].ClassListContains(LeaderboardPanel.LocalRowClass), Is.True);
            Assert.That(_container.Q<VisualElement>(LeaderboardPanel.RowsName).Query<VisualElement>(className: LeaderboardPanel.RowClass).ToList(), Has.Count.EqualTo(2));
            Assert.That(_panel.RefreshButton.enabledSelf, Is.True);
        }

        [Test]
        public void ShowsABoardButtonForEachBoardWithItsName()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open(new LeaderboardSpec(new[]
            {
                new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills),
                new LeaderboardDefinition("streak", null, PlayerStat.Kills, LeaderboardAggregation.Max, "BestKills"),
            }));

            Assert.That(_panel.BoardButtons.Select(b => b.text), Is.EqualTo(new[] { Text(new LocalizedText("leaderboard.kills")), "streak" }));
            Assert.That(_panel.BoardButtons[0].ClassListContains(LeaderboardPanel.SelectedBoardClass), Is.True);
            Assert.That(_panel.BoardButtons[1].ClassListContains(LeaderboardPanel.SelectedBoardClass), Is.False);
        }

        [Test]
        public void ChoosingAnotherBoardOpensItAndHidesTheRowsOfTheLastOne()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open();
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Ace", 3, false) }));

            Assert.That(_panel.Select("wins"), Is.True);

            Assert.That(_control.Opened, Is.EqualTo(new[] { "kills", "wins" }));
            Assert.That(_panel.RowTexts, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.LoadingKey))));
            Assert.That(_panel.BoardButtons[1].ClassListContains(LeaderboardPanel.SelectedBoardClass), Is.True);
            Assert.That(_panel.Select("nope"), Is.False);
            Assert.That(_control.Opened, Has.Count.EqualTo(2));
        }

        [Test]
        public void KeepsTheRowsWhileReloadingAndAsksForARefresh()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open();
            var rows = new[] { new LeaderboardRow(1, "Ace", 3, false) };
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Ready, rows));

            Assert.That(_panel.Refresh(), Is.True);
            Assert.That(_control.Refreshes, Is.EqualTo(1));

            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Loading, rows));

            Assert.That(_panel.RowTexts, Has.Count.EqualTo(1));
            Assert.That(_panel.Notice, Is.Null);
            Assert.That(_panel.RefreshButton.enabledSelf, Is.False, "no second refresh while loading");
        }

        [Test]
        public void SaysWhenTheBoardIsEmptyOrUnavailable()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open();

            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Ready, null));
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.EmptyKey))));

            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Unavailable, null));
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.UnavailableKey))));
            Assert.That(_panel.RowTexts, Is.Empty);
        }

        [Test]
        public void WithoutTheControlItSaysUnavailableAndOpensOnceItArrives()
        {
            Open();

            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.UnavailableKey))));
            Assert.That(_panel.RefreshButton.enabledSelf, Is.False);
            Assert.That(_panel.Refresh(), Is.False);

            GameSignals.LeaderboardControl.Publish(_control);

            Assert.That(_control.Opened, Is.EqualTo(new[] { "kills" }));
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.LoadingKey))));
        }

        [Test]
        public void SaysSoWhenNoBoardIsSetUp()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open(new LeaderboardSpec(new LeaderboardDefinition[0]));

            Assert.That(_panel.BoardButtons, Is.Empty);
            Assert.That(_control.Opened, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo(Text(new LocalizedText(LeaderboardText.NoBoardsKey))));
        }

        [Test]
        public void BackCancelsAndDisposeClosesTheBoard()
        {
            GameSignals.LeaderboardControl.Publish(_control);
            Open();

            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_panel.BackButton.text, Is.EqualTo(Text(LeaderboardText.Back)));

            _panel.Dispose();

            Assert.That(_control.Closes, Is.EqualTo(1));
            Assert.That(_container.childCount, Is.Zero);
            GameSignals.Leaderboard.Publish(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Ace", 3, false) }));
            Assert.That(_panel.RowTexts, Is.Empty, "a disposed panel does not draw");
        }

        private sealed class FakeLeaderboardControl : ILeaderboardControl
        {
            public List<string> Opened { get; } = new List<string>();

            public int Refreshes { get; private set; }

            public int Closes { get; private set; }

            public bool Open(string boardId)
            {
                Opened.Add(boardId);
                return true;
            }

            public bool Refresh()
            {
                Refreshes++;
                return true;
            }

            public void Close()
            {
                Closes++;
            }
        }
    }
}
