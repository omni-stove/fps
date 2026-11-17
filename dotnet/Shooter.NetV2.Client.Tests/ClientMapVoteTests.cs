using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientMapVoteTests
    {
        private static readonly int[] _sentOne = { 1 };
        private static readonly int[] _sentOneThenZero = { 1, 0 };

        // 送る知らせは使い回しなので、送ったときの番号を写しておく
        private readonly List<int> _sent = new List<int>();
        private bool _connected;
        private ClientMapVote _vote;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _connected = true;
            _vote = new ClientMapVote(message =>
            {
                if (!_connected)
                {
                    return false;
                }

                Assert.That(message, Is.InstanceOf<MapVoteRequestMessage>());
                _sent.Add(((MapVoteRequestMessage)message).MapIndex);
                return true;
            });
        }

        [Test]
        public void TheMessageBecomesAStatusWithTheCandidatesVotesAndSeconds()
        {
            Assert.That(_vote.Apply(State(12.5f, MapVoteStateMessage.NotChosen, ("NetArena02", 1), ("NetArena03", 0), ("NetArena05", 2))), Is.True);

            Assert.That(_vote.IsOpen, Is.True);
            Assert.That(
                _vote.Status,
                Is.EqualTo((MapVoteStatus?)new MapVoteStatus(
                    new[] { new MapVoteCandidate("NetArena02", 1), new MapVoteCandidate("NetArena03", 0), new MapVoteCandidate("NetArena05", 2) },
                    null,
                    12.5f,
                    null)));
        }

        [Test]
        public void AChosenIndexBecomesTheDecidedMapAndClosesTheVote()
        {
            _vote.Apply(State(0f, 2, ("NetArena02", 1), ("NetArena03", 0), ("NetArena05", 2)));

            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.Status.HasValue, Is.True);
            Assert.That(_vote.Status.Value.DecidedMapId, Is.EqualTo("NetArena05"));
        }

        [Test]
        public void NoCandidatesEndsTheVote()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));

            Assert.That(_vote.Apply(State(0f, MapVoteStateMessage.NotChosen)), Is.True);

            Assert.That(_vote.Status, Is.Null);
            Assert.That(_vote.IsOpen, Is.False);
        }

        [Test]
        public void AVoteIsSentAsTheCandidateIndexAndShownAsMine()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            Assert.That(_vote.Vote(1, out var sent), Is.EqualTo(MapVoteCast.Cast));

            Assert.That(sent, Is.True);
            Assert.That(_sent, Is.EqualTo(_sentOne));
            Assert.That(_vote.Status.Value.MyVote, Is.EqualTo("NetArena03"));
        }

        [Test]
        public void ChangingTheVoteSendsTheNewIndexAndTheSameChoiceIsNotSentAgain()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _vote.Vote(1, out _);

            Assert.That(_vote.Vote(1, out var again), Is.EqualTo(MapVoteCast.Unchanged));
            Assert.That(again, Is.False);
            Assert.That(_vote.Vote(0, out var changed), Is.EqualTo(MapVoteCast.Changed));
            Assert.That(changed, Is.True);

            Assert.That(_sent, Is.EqualTo(_sentOneThenZero));
            Assert.That(_vote.Status.Value.MyVote, Is.EqualTo("NetArena02"));
        }

        [Test]
        public void MyVoteSurvivesTheNextTallyOfTheSameCandidates()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _vote.Vote(1, out _);

            _vote.Apply(State(9f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 1)));
            Assert.That(_vote.Status.Value.MyVote, Is.EqualTo("NetArena03"));

            _vote.Apply(State(0f, 1, ("NetArena02", 0), ("NetArena03", 1)));
            Assert.That(_vote.Status.Value.MyVote, Is.EqualTo("NetArena03"));
        }

        [Test]
        public void ANewVoteAfterTheDecisionForgetsTheOldVote()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _vote.Vote(0, out _);
            _vote.Apply(State(0f, 0, ("NetArena02", 1), ("NetArena03", 0)));

            _vote.Apply(State(15f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            Assert.That(_vote.IsOpen, Is.True);
            Assert.That(_vote.Status.Value.HasVoted, Is.False);
        }

        [Test]
        public void OtherCandidatesForgetTheOldVote()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _vote.Vote(0, out _);

            _vote.Apply(State(15f, MapVoteStateMessage.NotChosen, ("NetArena04", 0), ("NetArena02", 0)));

            Assert.That(_vote.Status.Value.HasVoted, Is.False);
        }

        [Test]
        public void AFailedSendIsNotRemembered()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));
            _connected = false;

            Assert.That(_vote.Vote(0, out var sent), Is.EqualTo(MapVoteCast.Cast));

            Assert.That(sent, Is.False);
            Assert.That(_vote.Status.Value.HasVoted, Is.False);
        }

        [Test]
        public void NothingIsSentWithoutAnOpenVote()
        {
            Assert.That(_vote.Vote(0, out var beforeAny), Is.EqualTo(MapVoteCast.Closed));
            Assert.That(beforeAny, Is.False);

            _vote.Apply(State(0f, 0, ("NetArena02", 0), ("NetArena03", 0)));
            Assert.That(_vote.Vote(1, out var afterDecision), Is.EqualTo(MapVoteCast.Closed));
            Assert.That(afterDecision, Is.False);

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void AnIndexOutsideTheCandidatesIsNotSent()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            Assert.That(_vote.Vote(2, out var high), Is.EqualTo(MapVoteCast.NotCandidate));
            Assert.That(_vote.Vote(-1, out var low), Is.EqualTo(MapVoteCast.NotCandidate));

            Assert.That(high, Is.False);
            Assert.That(low, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void AVoteKeepsTheSecondsCountedSinceTheMessage()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0)));

            _vote.Tick(2.5f);
            _vote.Vote(0, out _);

            Assert.That(_vote.Status.Value.RemainingSeconds, Is.EqualTo(7.5f).Within(0.0001f));
        }

        [Test]
        public void TheSecondsStopAtZero()
        {
            _vote.Apply(State(1f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));

            _vote.Tick(5f);
            _vote.Vote(0, out _);

            Assert.That(_vote.Status.Value.RemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ARepeatedOrEmptyMapIdDropsTheVote()
        {
            _vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0)));

            Assert.That(_vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena02", 1))), Is.False);
            Assert.That(_vote.Status, Is.Null);

            Assert.That(_vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), (" ", 0))), Is.False);
            Assert.That(_vote.Status, Is.Null);
        }

        [Test]
        public void AChosenIndexOutsideTheCandidatesDropsTheVote()
        {
            Assert.That(_vote.Apply(State(0f, 3, ("NetArena02", 0), ("NetArena03", 0))), Is.False);

            Assert.That(_vote.Status, Is.Null);
        }

        [Test]
        public void TheStatusComesFromAMessageThatCameOverTheWire()
        {
            var message = State(8f, 1, ("NetArena02", 3), ("NetArena03", 4));
            var writer = new MessageWriter();
            message.Write(writer);
            var received = new MapVoteStateMessage();
            received.Read(new MessageReader(writer.ToArray()));

            _vote.Apply(received);

            Assert.That(_vote.Status.Value.Candidates, Is.EqualTo(new[] { new MapVoteCandidate("NetArena02", 3), new MapVoteCandidate("NetArena03", 4) }));
            Assert.That(_vote.Status.Value.RemainingSeconds, Is.EqualTo(8f));
            Assert.That(_vote.Status.Value.DecidedMapId, Is.EqualTo("NetArena03"));
        }

        [Test]
        public void TheSentIndexReadsBackOnTheServer()
        {
            MapVoteRequestMessage request = null;
            var vote = new ClientMapVote(message =>
            {
                var writer = new MessageWriter();
                message.Write(writer);
                request = new MapVoteRequestMessage();
                request.Read(new MessageReader(writer.ToArray()));
                return true;
            });
            vote.Apply(State(10f, MapVoteStateMessage.NotChosen, ("NetArena02", 0), ("NetArena03", 0), ("NetArena05", 0)));

            vote.Vote(2, out _);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.MapIndex, Is.EqualTo(2));
        }

        [Test]
        public void RefusesNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientMapVote(null));
            Assert.Throws<ArgumentNullException>(() => _vote.Apply(null));
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
