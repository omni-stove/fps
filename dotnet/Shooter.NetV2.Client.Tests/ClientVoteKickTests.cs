using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientVoteKickTests
    {
        private readonly List<ClientVoteKickState> _received = new List<ClientVoteKickState>();
        private ClientVoteKick _voteKick;

        [SetUp]
        public void SetUp()
        {
            _received.Clear();
            _voteKick = new ClientVoteKick();
            _voteKick.StateReceived += _received.Add;
        }

        [Test]
        public void AReceivedVoteKickStateRaisesTheEventWithItsValues()
        {
            var handled = _voteKick.Handle(Read(new VoteKickStateMessage
            {
                TargetPlayerId = 4,
                CallerPlayerId = 2,
                YesVotes = 1,
                RequiredVotes = 3,
                RemainingSeconds = 25.5f,
                Outcome = VoteKickOutcome.Undecided,
            }));

            Assert.That(handled, Is.True);
            Assert.That(_received, Has.Count.EqualTo(1));
            var state = _received[0];
            Assert.That(state.TargetPlayerId, Is.EqualTo(4));
            Assert.That(state.CallerPlayerId, Is.EqualTo(2));
            Assert.That(state.YesVotes, Is.EqualTo(1));
            Assert.That(state.RequiredVotes, Is.EqualTo(3));
            Assert.That(state.RemainingSeconds, Is.EqualTo(25.5f));
            Assert.That(state.Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
        }

        [Test]
        public void EachStateRaisesItsOwnEventAndKeepsItsValuesAfterTheTableReusesTheMessage()
        {
            var table = MatchMessages.CreateClientTable();
            _voteKick.Handle(Read(table, new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 2, RemainingSeconds = 10f }));
            _voteKick.Handle(Read(table, new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 2, RequiredVotes = 2, Outcome = VoteKickOutcome.Passed }));

            Assert.That(_received, Has.Count.EqualTo(2));
            Assert.That(_received[0].YesVotes, Is.EqualTo(1));
            Assert.That(_received[0].RemainingSeconds, Is.EqualTo(10f));
            Assert.That(_received[0].Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
            Assert.That(_received[1].YesVotes, Is.EqualTo(2));
            Assert.That(_received[1].RemainingSeconds, Is.Zero);
            Assert.That(_received[1].Outcome, Is.EqualTo(VoteKickOutcome.Passed));
        }

        [Test]
        public void OtherMessagesAreLeftToOthersAndRaiseNothing()
        {
            Assert.That(_voteKick.Handle(new MapVoteStateMessage()), Is.False);
            Assert.That(_received, Is.Empty);
        }

        [Test]
        public void ANullStateIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _voteKick.Receive(null));
        }

        [Test]
        public void NothingIsOpenBeforeAStateArrives()
        {
            Assert.That(_voteKick.Status, Is.Null);
            Assert.That(_voteKick.IsOpen, Is.False);
        }

        [Test]
        public void AnUndecidedStateBecomesTheStatus()
        {
            _voteKick.Handle(Read(new VoteKickStateMessage
            {
                TargetPlayerId = 4,
                CallerPlayerId = 2,
                YesVotes = 1,
                RequiredVotes = 3,
                RemainingSeconds = 25.5f,
            }));

            Assert.That(_voteKick.IsOpen, Is.True);
            Assert.That(_voteKick.Status, Is.EqualTo((VoteKickStatus?)new VoteKickStatus(4, 2, 1, 3, 25.5f)));
        }

        [TestCase(VoteKickOutcome.Passed)]
        [TestCase(VoteKickOutcome.Failed)]
        public void ADecidedStateClosesTheVote(VoteKickOutcome outcome)
        {
            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 2, RemainingSeconds = 10f }));

            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 2, RequiredVotes = 2, Outcome = outcome }));

            Assert.That(_voteKick.Status, Is.Null);
            Assert.That(_received, Has.Count.EqualTo(2), "the decided state is still raised so the outcome can be heard");
        }

        [Test]
        public void ToStatusIsEmptyForADecidedStateAndCopiesAnUndecidedOne()
        {
            Assert.That(
                ClientVoteKick.ToStatus(new VoteKickStateMessage { TargetPlayerId = 1, CallerPlayerId = 0, YesVotes = 0, RequiredVotes = 1, RemainingSeconds = 5f }),
                Is.EqualTo((VoteKickStatus?)new VoteKickStatus(1, 0, 0, 1, 5f)));
            Assert.That(ClientVoteKick.ToStatus(new VoteKickStateMessage { TargetPlayerId = 1, Outcome = VoteKickOutcome.Failed }), Is.Null);
            Assert.Throws<ArgumentNullException>(() => ClientVoteKick.ToStatus(null));
        }

        [Test]
        public void AStateWithValuesOutOfRangeClosesTheVoteInsteadOfThrowing()
        {
            _voteKick.Receive(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 0, RequiredVotes = 2, RemainingSeconds = 10f });

            var wellFormed = _voteKick.Receive(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = -1, RequiredVotes = 2, RemainingSeconds = 10f });

            Assert.That(wellFormed, Is.False);
            Assert.That(_voteKick.Status, Is.Null);
            Assert.That(_voteKick.Receive(new VoteKickStateMessage { TargetPlayerId = 4, RequiredVotes = 0 }), Is.False);
            Assert.That(_voteKick.Receive(new VoteKickStateMessage { TargetPlayerId = 4, RemainingSeconds = float.NaN }), Is.False);
            Assert.That(_voteKick.Receive(new VoteKickStateMessage { TargetPlayerId = 4, Outcome = (VoteKickOutcome)9 }), Is.False);
        }

        [Test]
        public void ACallRequestIsAVoteInFavorOfKickingTheTarget()
        {
            var request = _voteKick.CallRequest(6);

            Assert.That(request.TargetPlayerId, Is.EqualTo(6));
            Assert.That(request.InFavor, Is.True);
            var read = (VoteKickRequestMessage)ReadOnServer(request);
            Assert.That(read.TargetPlayerId, Is.EqualTo(6));
            Assert.That(read.InFavor, Is.True);
        }

        [Test]
        public void ACallOnANegativeIdIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _voteKick.CallRequest(-1));
        }

        [Test]
        public void AVoteRequestNeedsAVoteInProgressAndTargetsItsPlayer()
        {
            Assert.That(_voteKick.VoteRequest(true), Is.Null);

            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 3, RemainingSeconds = 20f }));
            var yes = _voteKick.VoteRequest(true);
            var no = _voteKick.VoteRequest(false);

            Assert.That(yes.TargetPlayerId, Is.EqualTo(4));
            Assert.That(yes.InFavor, Is.True);
            Assert.That(no.TargetPlayerId, Is.EqualTo(4));
            Assert.That(no.InFavor, Is.False);
            Assert.That(no, Is.Not.SameAs(yes), "each request is its own message");
        }

        [Test]
        public void NoVoteRequestIsMadeAfterTheVoteIsDecidedOrReset()
        {
            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 3, RemainingSeconds = 20f }));
            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 4, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 3, Outcome = VoteKickOutcome.Failed }));
            Assert.That(_voteKick.VoteRequest(true), Is.Null);

            _voteKick.Handle(Read(new VoteKickStateMessage { TargetPlayerId = 5, CallerPlayerId = 2, YesVotes = 1, RequiredVotes = 3, RemainingSeconds = 20f }));
            _voteKick.Reset();
            Assert.That(_voteKick.VoteRequest(false), Is.Null);
            Assert.That(_voteKick.Status, Is.Null);
        }

        private static IMessage ReadOnServer(IMessage sent)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            return MatchMessages.CreateServerTable().Read(writer.AsSegment());
        }

        private static IMessage Read(VoteKickStateMessage sent)
        {
            return Read(MatchMessages.CreateClientTable(), sent);
        }

        private static IMessage Read(MessageTable table, VoteKickStateMessage sent)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            return table.Read(writer.AsSegment());
        }
    }
}
