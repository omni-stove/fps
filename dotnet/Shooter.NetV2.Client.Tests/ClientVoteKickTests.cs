using System;
using System.Collections.Generic;
using NUnit.Framework;
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
