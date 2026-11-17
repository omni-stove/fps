using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MapVoteViewTests
    {
        private Hud _hud;

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }

                _hud = null;
            }
        }

        [Test]
        public void TheStyleSheetIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(MapVoteView.StyleSheetResourcePath), Is.Not.Null);
        }

        [Test]
        public void TheViewIsHiddenUntilAVoteIsPublished()
        {
            var view = CreateView();

            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Panel, Is.Not.Null);
            Assert.That(view.Panel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.CandidateButtons, Is.Empty);
            Assert.That(_hud.GetComponents<MapVoteView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ThePanelSitsInFrontOfTheMatchResult()
        {
            var view = CreateView();
            var root = view.Element;
            var result = root.Q<VisualElement>("hud-result-restart");

            Assert.That(view.Panel.parent, Is.SameAs(root));
            Assert.That(root.IndexOf(view.Panel), Is.EqualTo(root.IndexOf(result) + 1));
            Assert.That(view.Panel.ClassListContains(MapVoteView.PanelClass), Is.True);
        }

        [Test]
        public void APublishedVoteShowsEachCandidateWithItsNameVotesAndTheTimeLeft()
        {
            var view = CreateView();

            GameSignals.MapVote.Publish(Vote(12.3f, myVote: null, decided: null, 2, 1, 0));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Panel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.TitleText, Is.EqualTo("次のマップを選ぶ"));
            Assert.That(view.TimerText, Is.EqualTo("残り 13 秒"), "a part second still counts as a second");
            Assert.That(view.CandidateButtons, Has.Count.EqualTo(3));
            Assert.That(Texts(view, MapVoteView.NameClass), Is.EqualTo(new[] { "テスト場", "アリーナ", "dust-yard" }), "a map without a name in the table shows its Id");
            Assert.That(Texts(view, MapVoteView.VotesClass), Is.EqualTo(new[] { "2 票", "1 票", "0 票" }));
            Assert.That(view.CandidateButtons.All(button => button.enabledSelf), Is.True);
            Assert.That(view.CandidateButtons.Any(button => button.ClassListContains(MapVoteView.MineClass)), Is.False);
        }

        [Test]
        public void ChoosingACandidateCallsTheSenderWithItsIndex()
        {
            var view = CreateView();
            var sender = PublishSender();
            var voted = new List<int>();
            view.Voted += voted.Add;
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            Assert.That(view.Choose(2), Is.True);
            Assert.That(view.Choose(0), Is.True, "the vote can be changed before it closes");

            Assert.That(sender.Votes, Is.EqualTo(new[] { 2, 0 }));
            Assert.That(voted, Is.EqualTo(new[] { 2, 0 }), "Voted tells what was sent");
        }

        [Test]
        public void TheSenderPublishedLatestIsCalled()
        {
            var view = CreateView();
            var first = PublishSender();
            var second = PublishSender();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            Assert.That(view.Choose(1), Is.True);

            Assert.That(first.Votes, Is.Empty);
            Assert.That(second.Votes, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void WithoutASenderNothingIsChosen()
        {
            var view = CreateView();
            var voted = new List<int>();
            view.Voted += voted.Add;
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            LogAssert.Expect(LogType.Warning, new Regex("no vote sender is published"));
            Assert.That(view.Choose(1), Is.False);

            Assert.That(voted, Is.Empty);
        }

        [Test]
        public void AWithdrawnSenderIsNotCalled()
        {
            var view = CreateView();
            var sender = PublishSender();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            GameSignals.MapVoteSender.ClearIfCurrent(sender);

            LogAssert.Expect(LogType.Warning, new Regex("no vote sender is published"));
            Assert.That(view.Choose(0), Is.False);
            Assert.That(sender.Votes, Is.Empty);
        }

        [Test]
        public void TheCandidateAlreadyVotedForIsMarkedAndNotSentAgain()
        {
            var view = CreateView();
            var sender = PublishSender();

            GameSignals.MapVote.Publish(Vote(20f, myVote: "NetArena01", decided: null, 0, 1, 0));

            Assert.That(view.CandidateButtons[1].ClassListContains(MapVoteView.MineClass), Is.True);
            Assert.That(view.CandidateButtons[0].ClassListContains(MapVoteView.MineClass), Is.False);
            Assert.That(view.Choose(1), Is.False);
            Assert.That(sender.Votes, Is.Empty);
        }

        [Test]
        public void NewVoteCountsKeepTheSameButtonsSoTheFocusStays()
        {
            var view = CreateView();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));
            var before = view.CandidateButtons.ToArray();

            GameSignals.MapVote.Publish(Vote(15f, myVote: null, decided: null, 1, 3, 0));

            Assert.That(view.CandidateButtons, Is.EqualTo(before));
            Assert.That(Texts(view, MapVoteView.VotesClass), Is.EqualTo(new[] { "1 票", "3 票", "0 票" }));
            Assert.That(view.TimerText, Is.EqualTo("残り 15 秒"));
        }

        [Test]
        public void OtherCandidatesReplaceTheButtons()
        {
            var view = CreateView();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            GameSignals.MapVote.Publish(new MapVoteStatus(new[] { new MapVoteCandidate("NetTest", 0) }, null, 10f, null));

            Assert.That(view.CandidateButtons, Has.Count.EqualTo(1));
            Assert.That(view.Panel.Query<Button>(className: MapVoteView.CandidateClass).ToList(), Has.Count.EqualTo(1));
            Assert.That(Texts(view, MapVoteView.NameClass), Is.EqualTo(new[] { "テスト場" }));
        }

        [Test]
        public void ADecidedVoteNamesTheNextMapAndTakesNoMoreVotes()
        {
            var view = CreateView();
            var sender = PublishSender();

            GameSignals.MapVote.Publish(Vote(0f, myVote: "NetTest", decided: "NetArena01", 1, 2, 0));

            Assert.That(view.TitleText, Is.EqualTo("次のマップ: アリーナ"));
            Assert.That(view.TimerText, Is.Empty);
            Assert.That(view.CandidateButtons.All(button => !button.enabledSelf), Is.True);
            Assert.That(view.CandidateButtons[1].ClassListContains(MapVoteView.DecidedCandidateClass), Is.True);
            Assert.That(view.Choose(2), Is.False);
            Assert.That(sender.Votes, Is.Empty);
        }

        [Test]
        public void ClearingTheVoteHidesThePanel()
        {
            var view = CreateView();
            var sender = PublishSender();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            GameSignals.MapVote.Clear();

            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Panel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.CandidateButtons, Is.Empty);
            Assert.That(view.Choose(0), Is.False);
            Assert.That(sender.Votes, Is.Empty);
        }

        [Test]
        public void AVoteWithoutCandidatesIsNotShown()
        {
            var view = CreateView();

            GameSignals.MapVote.Publish(new MapVoteStatus(null, null, 10f, null));

            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Panel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ChoosingOutsideTheCandidatesThrows()
        {
            var view = CreateView();
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() => view.Choose(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.Choose(-1));
        }

        [Test]
        public void AVotePublishedBeforeTheViewIsBoundIsShownOnBind()
        {
            GameSignals.MapVote.Publish(Vote(5f, myVote: null, decided: null, 0, 0, 0));

            var view = CreateView();

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.CandidateButtons, Has.Count.EqualTo(3));
        }

        [Test]
        public void UnbindingTakesThePanelOutOfTheHud()
        {
            var view = CreateView();
            var root = view.Element;
            GameSignals.MapVote.Publish(Vote(20f, myVote: null, decided: null, 0, 0, 0));

            view.Unbind();

            Assert.That(root.Q<VisualElement>(MapVoteView.PanelName), Is.Null);
            Assert.That(view.Panel, Is.Null);
            Assert.That(view.CandidateButtons, Is.Empty);
        }

        [TestCase(0f, 0)]
        [TestCase(0.2f, 1)]
        [TestCase(3f, 3)]
        [TestCase(3.01f, 4)]
        public void TheTimeLeftIsRoundedUpToWholeSeconds(float seconds, int expected)
        {
            Assert.That(MapVoteView.WholeSeconds(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void EveryMapVoteKeyIsInBothLanguages()
        {
            var keys = new[]
            {
                MapVoteView.TitleKey,
                MapVoteView.RemainingKey,
                MapVoteView.VotesKey,
                MapVoteView.DecidedKey,
                MapVoteView.MapNameKey("NetTest"),
                MapVoteView.MapNameKey("NetArena01"),
            };
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }

        private MapVoteView CreateView()
        {
            _hud = Hud.Create("MapVoteTests.Hud");
            var view = _hud.MapVote;
            Assert.That(view, Is.Not.Null, "Hud.AddViews attaches the map vote");
            Assert.That(view.Element, Is.Not.Null);
            return view;
        }

        private static FakeMapVoteSender PublishSender()
        {
            var sender = new FakeMapVoteSender();
            GameSignals.MapVoteSender.Publish(sender);
            return sender;
        }

        private static MapVoteStatus Vote(float remainingSeconds, string myVote, string decided, int testVotes, int arenaVotes, int yardVotes)
        {
            var candidates = new[]
            {
                new MapVoteCandidate("NetTest", testVotes),
                new MapVoteCandidate("NetArena01", arenaVotes),
                new MapVoteCandidate("dust-yard", yardVotes),
            };
            return new MapVoteStatus(candidates, myVote, remainingSeconds, decided);
        }

        private static string[] Texts(MapVoteView view, string className)
        {
            return view.CandidateButtons.Select(button => button.Q<Label>(className: className).text).ToArray();
        }

        private sealed class FakeMapVoteSender : IMapVoteSender
        {
            public List<int> Votes { get; } = new List<int>();

            public void Vote(int candidateIndex)
            {
                Votes.Add(candidateIndex);
            }
        }
    }
}
