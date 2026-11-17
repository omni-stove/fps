using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2MapVotePartTests
    {
        private readonly List<int> _sent = new List<int>();
        private readonly List<string> _logs = new List<string>();
        private NetV2MapVotePart _part;
        private bool _connected;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            GameSignals.MapVote.Clear();
            GameSignals.MapVoteSender.Clear();
            _sent.Clear();
            _logs.Clear();
            _connected = true;
            _part = new NetV2MapVotePart(Send, _logs.Add);
            _changes = 0;
            GameSignals.MapVote.Changed += CountChange;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.MapVote.Changed -= CountChange;
            _part.Dispose();
            GameSignals.MapVote.Clear();
            GameSignals.MapVoteSender.Clear();
        }

        [Test]
        public void TheStateMessageIsTakenAndShownWithTheSender()
        {
            Assert.That(_part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 1), ("NetArena03", 0))), Is.True);

            var status = GameSignals.MapVote.Value;
            Assert.That(status.HasValue, Is.True);
            Assert.That(status.Value.Candidates, Is.EqualTo(new[] { new MapVoteCandidate("NetArena02", 1), new MapVoteCandidate("NetArena03", 0) }));
            Assert.That(status.Value.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(status.Value.IsDecided, Is.False);
            Assert.That(GameSignals.MapVoteSender.Value, Is.SameAs(_part.MapVoteSender));
            Assert.That(_logs, Is.EqualTo(new[] { MapVoteLog.ClientChanged(status) }));
        }

        [Test]
        public void OtherMessagesAreLeftToTheRest()
        {
            Assert.That(_part.Handle(new MapVoteRequestMessage()), Is.False);
            Assert.That(GameSignals.MapVote.HasValue, Is.False);
        }

        [Test]
        public void VotingThroughTheSenderSendsTheIndexAndShowsItAsMine()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            GameSignals.MapVoteSender.Value.Vote(1);

            Assert.That(_sent, Is.EqualTo(new[] { 1 }));
            Assert.That(GameSignals.MapVote.Value.Value.MyVote, Is.EqualTo("NetArena03"));
            Assert.That(_logs, Has.Member(MapVoteLog.ClientRequested(1, "NetArena03", MapVoteCast.Cast, true)));
        }

        [Test]
        public void ChoosingOnTheHudSendsTheIndexAndMarksItAsMine()
        {
            var hud = Hud.Create("NetV2MapVotePartTests.Hud");
            try
            {
                _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
                var view = hud.MapVote;
                var buttonsBefore = view.CandidateButtons.ToArray();

                Assert.That(view.Choose(1), Is.True);

                Assert.That(_sent, Is.EqualTo(new[] { 1 }));
                Assert.That(view.Status.Value.MyVote, Is.EqualTo("NetArena03"));
                Assert.That(view.CandidateButtons, Is.EqualTo(buttonsBefore), "showing my vote keeps the buttons");
                Assert.That(view.CandidateButtons[1].ClassListContains(MapVoteView.MineClass), Is.True);
                Assert.That(view.Choose(1), Is.False, "the same vote is not sent again");
                Assert.That(_sent, Is.EqualTo(new[] { 1 }));
            }
            finally
            {
                var settings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }
        }

        [Test]
        public void AVoteThatCouldNotBeSentIsLoggedAndNotShown()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _connected = false;
            var before = _changes;

            _part.MapVoteSender.Vote(0);

            Assert.That(_changes, Is.EqualTo(before));
            Assert.That(GameSignals.MapVote.Value.Value.HasVoted, Is.False);
            Assert.That(_logs, Has.Member(MapVoteLog.ClientRequested(0, "NetArena02", MapVoteCast.Cast, false)));
        }

        [Test]
        public void TheSameTallyIsNotPublishedAgain()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));

            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void TheDecisionClearsTheVoteAndTheSender()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            _part.Handle(State(0f, 1, ("NetArena02", 0), ("NetArena03", 1)));

            Assert.That(GameSignals.MapVote.HasValue, Is.False);
            Assert.That(GameSignals.MapVoteSender.HasValue, Is.False);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(MapVoteLog.ClientChanged(null)));
        }

        [Test]
        public void AVoteAfterTheDecisionIsNotSent()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            var sender = _part.MapVoteSender;
            _part.Handle(State(0f, 0, ("NetArena02", 0), ("NetArena03", 0)));

            sender.Vote(1);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(MapVoteLog.ClientRequested(1, "NetArena03", MapVoteCast.Closed, false)));
        }

        [Test]
        public void AMalformedMessageIsLoggedAndClearsTheVote()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));

            Assert.That(_part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena02", 0))), Is.True);

            Assert.That(GameSignals.MapVote.HasValue, Is.False);
            Assert.That(GameSignals.MapVoteSender.HasValue, Is.False);
            Assert.That(_logs, Has.Member(MapVoteLog.ClientMalformed(2)));
        }

        [Test]
        public void DisposingClearsOnlyWhatThePartPublished()
        {
            _part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));
            _part.Dispose();
            Assert.That(GameSignals.MapVote.HasValue, Is.False);
            Assert.That(GameSignals.MapVoteSender.HasValue, Is.False);

            var other = new MapVoteStatus(new[] { new MapVoteCandidate("NetArena04", 0) }, null, 5f, null);
            GameSignals.MapVote.Publish(other);
            var otherPart = new NetV2MapVotePart(Send, _logs.Add);
            otherPart.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena05", 0)));
            GameSignals.MapVote.Publish(other);
            otherPart.Dispose();

            Assert.That(GameSignals.MapVote.Value, Is.EqualTo((MapVoteStatus?)other));
        }

        [Test]
        public void AfterDisposingMessagesAreTakenButNotShown()
        {
            _part.Dispose();

            Assert.That(_part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0))), Is.True);

            Assert.That(GameSignals.MapVote.HasValue, Is.False);
            Assert.That(GameSignals.MapVoteSender.HasValue, Is.False);
        }

        [Test]
        public void EveryStateTellsTheDecidedMapAndEmptyUntilItIsDecided()
        {
            var decided = new List<string>();
            var part = new NetV2MapVotePart(Send, _logs.Add, decided.Add);

            part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            part.Handle(State(0f, 1, ("NetArena02", 0), ("NetArena03", 1)));
            part.Handle(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena02", 0)));
            part.Dispose();
            part.Handle(State(0f, 0, ("NetArena02", 1)));

            Assert.That(decided, Is.EqualTo(new[] { "", "NetArena03", "" }), "a malformed vote closes it, and nothing is told after disposing");
        }

        [Test]
        public void RefusesANullSend()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2MapVotePart(null));
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            _sent.Add(((MapVoteRequestMessage)message).MapIndex);
            return true;
        }

        private void CountChange(MapVoteStatus? status)
        {
            _changes++;
        }

        private static MapVoteStateMessage State(float seconds, int chosenIndex, params (string MapId, int Votes)[] candidates)
        {
            var message = new MapVoteStateMessage { RemainingSeconds = seconds };
            foreach (var candidate in candidates)
            {
                message.AddCandidate(candidate.MapId, candidate.Votes);
            }

            message.ChosenIndex = chosenIndex;
            return message;
        }
    }
}
