using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Hud にはまだ付けていないので、Hud.uxml を複製した木を直接 Bind する。
    // 木はパネルに付いていないのでボタンを押す知らせが届かず、ボタンが呼ぶ Vote を直接呼んで確かめる。
    public sealed class VoteKickViewTests
    {
        private const int _localId = 0;
        private const int _callerId = 2;
        private const int _targetId = 5;

        private readonly List<Object> _created = new List<Object>();
        private VoteKickView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _view = CreateView();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void TheStyleSheetIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(VoteKickView.StyleSheetResourcePath), Is.Not.Null);
        }

        [Test]
        public void TheViewIsHiddenWithoutAVote()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the vote-kick element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.TitleText, Is.Empty);
            Assert.That(_view.TimerText, Is.Empty);
        }

        [Test]
        public void APublishedVoteShowsTheTargetTheTallyTheTimeLeftAndTheChoices()
        {
            PublishPlayers();
            PublishSender();

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 12.3f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.TitleText, Is.EqualTo("Player 2 が Player 5 のキックの投票を始めた"));
            Assert.That(_view.TallyText, Is.EqualTo("1 / 3"));
            Assert.That(_view.TimerText, Is.EqualTo("残り 13 秒"), "a part second still counts as a second");
            Assert.That(_view.ChoicesShown, Is.True);
            Assert.That(_view.YesButton.text, Is.EqualTo("賛成"));
            Assert.That(_view.NoButton.text, Is.EqualTo("反対"));
            Assert.That(_view.YesButton.ClassListContains(VoteKickView.YesClass), Is.True);
            Assert.That(_view.NoButton.ClassListContains(VoteKickView.NoClass), Is.True);
        }

        [Test]
        public void ANewStateReplacesTheTallyAndTheTimeLeft()
        {
            PublishPlayers();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 2, 3, 7.5f));

            Assert.That(_view.TallyText, Is.EqualTo("2 / 3"));
            Assert.That(_view.TimerText, Is.EqualTo("残り 8 秒"));
        }

        [TestCase(0f, "残り 0 秒")]
        [TestCase(0.2f, "残り 1 秒")]
        [TestCase(3f, "残り 3 秒")]
        [TestCase(3.01f, "残り 4 秒")]
        public void TheTimeLeftIsShownInWholeSecondsRoundedUp(float seconds, string expected)
        {
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 0, 2, seconds));

            Assert.That(_view.TimerText, Is.EqualTo(expected));
        }

        [Test]
        public void NamesComeFromTheScoreboardAndFallBackToThePlayerNumber()
        {
            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(_callerId, ScoreboardName.Literal("Ann"), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, false),
            });

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 2, 10f));

            Assert.That(_view.TitleText, Is.EqualTo("Ann が Player 5 のキックの投票を始めた"), "the target is not on the list");

            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(_callerId, ScoreboardName.Literal("Ann"), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, false),
                new ScoreboardEntry(_targetId, ScoreboardName.Literal("Bob"), Team.Bravo, 0, 0, 0, ScoreboardConnection.Connected, false),
            });

            Assert.That(_view.TitleText, Is.EqualTo("Ann が Bob のキックの投票を始めた"), "a list that arrives later renames them");
        }

        [Test]
        public void ClearingTheVoteHidesTheView()
        {
            PublishPlayers();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            GameSignals.VoteKick.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.TimerText, Is.Empty);
        }

        [Test]
        public void AVotePublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            var late = CreateView();

            Assert.That(late.IsShown, Is.True);
            Assert.That(late.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(late.TallyText, Is.EqualTo("1 / 3"));
        }

        [Test]
        public void VotingSendsTheChoiceOnceAndMarksIt()
        {
            PublishPlayers();
            var sender = PublishSender();
            var voted = new List<bool>();
            _view.Voted += voted.Add;
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            Assert.That(_view.Vote(true), Is.True);
            Assert.That(_view.Vote(true), Is.False, "the same choice is not sent again");
            Assert.That(_view.YesButton.ClassListContains(VoteKickView.MineClass), Is.True);
            Assert.That(_view.NoButton.ClassListContains(VoteKickView.MineClass), Is.False);

            Assert.That(_view.Vote(false), Is.True, "the choice can be changed");

            Assert.That(sender.Votes, Is.EqualTo(new[] { true, false }));
            Assert.That(voted, Is.EqualTo(new[] { true, false }));
            Assert.That(_view.MyVote, Is.False);
            Assert.That(_view.NoButton.ClassListContains(VoteKickView.MineClass), Is.True);
            Assert.That(_view.YesButton.ClassListContains(VoteKickView.MineClass), Is.False);
        }

        [Test]
        public void ANewTallyForTheSameVoteKeepsTheChoice()
        {
            PublishPlayers();
            PublishSender();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));
            _view.Vote(false);

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 2, 3, 15f));

            Assert.That(_view.MyVote, Is.False);
            Assert.That(_view.NoButton.ClassListContains(VoteKickView.MineClass), Is.True);
        }

        [Test]
        public void AnotherVoteForgetsTheChoice()
        {
            PublishPlayers();
            PublishSender();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));
            _view.Vote(true);

            GameSignals.VoteKick.Clear();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_callerId, _targetId, 1, 3, 20f));

            Assert.That(_view.MyVote, Is.Null);
            Assert.That(_view.YesButton.ClassListContains(VoteKickView.MineClass), Is.False);
        }

        [Test]
        public void TheCallerHasAlreadyVotedYes()
        {
            PublishPlayers();
            var sender = PublishSender();

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _localId, 1, 3, 20f));

            Assert.That(_view.MyVote, Is.True);
            Assert.That(_view.YesButton.ClassListContains(VoteKickView.MineClass), Is.True);
            Assert.That(_view.Vote(true), Is.False);
            Assert.That(sender.Votes, Is.Empty);
        }

        [Test]
        public void TheTargetSeesTheVoteButCannotVote()
        {
            PublishPlayers();
            var sender = PublishSender();

            GameSignals.VoteKick.Publish(new VoteKickStatus(_localId, _callerId, 1, 3, 20f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.IsLocalTarget, Is.True);
            Assert.That(_view.TitleText, Is.EqualTo("Player 2 が Player 0 のキックの投票を始めた"));
            Assert.That(_view.ChoicesShown, Is.False);
            Assert.That(_view.Element.ClassListContains(VoteKickView.TargetIsLocalClass), Is.True);
            Assert.That(_view.Vote(false), Is.False);
            Assert.That(sender.Votes, Is.Empty);

            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            Assert.That(_view.Element.ClassListContains(VoteKickView.TargetIsLocalClass), Is.False);
            Assert.That(_view.ChoicesShown, Is.True);
        }

        [Test]
        public void WithoutASenderTheChoicesAreHiddenAndNothingIsSent()
        {
            PublishPlayers();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            Assert.That(_view.IsShown, Is.True, "the vote is still shown");
            Assert.That(_view.ChoicesShown, Is.False);
            Assert.That(_view.CanVote, Is.False);

            LogAssert.Expect(LogType.Warning, new Regex("no sender is published"));
            Assert.That(_view.Vote(true), Is.False);
            Assert.That(_view.MyVote, Is.Null);
        }

        [Test]
        public void ASenderPublishedDuringTheVoteShowsTheChoicesAndAWithdrawnOneHidesThem()
        {
            PublishPlayers();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            var sender = PublishSender();

            Assert.That(_view.ChoicesShown, Is.True);

            GameSignals.VoteKickSender.ClearIfCurrent(sender);

            Assert.That(_view.ChoicesShown, Is.False);
        }

        [Test]
        public void UnbindingEmptiesTheElementAndStopsListening()
        {
            var element = _view.Element;
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 1, 3, 20f));

            _view.Unbind();
            GameSignals.VoteKick.Publish(new VoteKickStatus(_targetId, _callerId, 2, 3, 10f));

            Assert.That(element.childCount, Is.Zero);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Status.Value.YesVotes, Is.EqualTo(1));
        }

        [Test]
        public void TheTallyIsTheYesVotesOverTheVotesNeeded()
        {
            Assert.That(VoteKickView.Tally(0, 1), Is.EqualTo("0 / 1"));
            Assert.That(VoteKickView.Tally(4, 12), Is.EqualTo("4 / 12"));
        }

        private static void PublishPlayers()
        {
            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(_localId, ScoreboardName.Player(_localId), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, true),
                new ScoreboardEntry(_callerId, ScoreboardName.Player(_callerId), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, false),
                new ScoreboardEntry(_targetId, ScoreboardName.Player(_targetId), Team.Bravo, 0, 0, 0, ScoreboardConnection.Connected, false),
            });
        }

        private static FakeVoteKickSender PublishSender()
        {
            var sender = new FakeVoteKickSender();
            GameSignals.VoteKickSender.Publish(sender);
            return sender;
        }

        private VoteKickView CreateView()
        {
            var host = new GameObject("TestVoteKick");
            _created.Add(host);
            var view = host.AddComponent<VoteKickView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            view.Bind(layout.CloneTree());
            return view;
        }

        private sealed class FakeVoteKickSender : IVoteKickSender
        {
            public List<bool> Votes { get; } = new List<bool>();

            public void Call(int targetId)
            {
            }

            public void Vote(bool inFavor)
            {
                Votes.Add(inFavor);
            }
        }
    }
}
