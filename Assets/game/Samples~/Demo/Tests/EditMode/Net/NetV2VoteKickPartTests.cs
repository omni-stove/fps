using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
// サーバーの書式の Shooter.Core.VoteKickLog と同じ名前なので、クライアントの書式を名前で選ぶ
using VoteKickLog = Shooter.NetV2.VoteKickLog;

namespace Shooter.Net.Tests
{
    public sealed class NetV2VoteKickPartTests
    {
        private readonly List<VoteKickRequestMessage> _sent = new List<VoteKickRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private NetV2VoteKickPart _part;
        private bool _connected;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            GameSignals.VoteKick.Clear();
            GameSignals.VoteKickSender.Clear();
            _sent.Clear();
            _logs.Clear();
            _connected = true;
            _part = new NetV2VoteKickPart(Send, _logs.Add);
            _changes = 0;
            GameSignals.VoteKick.Changed += CountChange;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.VoteKick.Changed -= CountChange;
            _part.Dispose();
            GameSignals.VoteKick.Clear();
            GameSignals.VoteKickSender.Clear();
        }

        [Test]
        public void NothingIsPublishedBeforeTheMatchStarts()
        {
            Assert.That(GameSignals.VoteKick.HasValue, Is.False);
            Assert.That(GameSignals.VoteKickSender.HasValue, Is.False);
        }

        [Test]
        public void TheFirstSnapshotPublishesTheSender()
        {
            _part.Apply(new SnapshotMessage());

            Assert.That(GameSignals.VoteKickSender.Value, Is.SameAs(_part.VoteKickSender));
        }

        [Test]
        public void AStateIsTakenAndShown()
        {
            Assert.That(_part.Handle(State(4, 2, 1, 3, 25f)), Is.True);

            var expected = new VoteKickStatus(4, 2, 1, 3, 25f);
            Assert.That(GameSignals.VoteKick.Value, Is.EqualTo((VoteKickStatus?)expected));
            Assert.That(_logs, Is.EqualTo(new[] { VoteKickLog.Changed(expected) }));
        }

        [Test]
        public void ANewTallyReplacesTheShownOneAndTheSameTallyIsNotPublishedAgain()
        {
            _part.Handle(State(4, 2, 1, 3, 25f));
            _part.Handle(State(4, 2, 1, 3, 25f));
            _part.Handle(State(4, 2, 2, 3, 20f));

            Assert.That(_changes, Is.EqualTo(2));
            Assert.That(GameSignals.VoteKick.Value, Is.EqualTo((VoteKickStatus?)new VoteKickStatus(4, 2, 2, 3, 20f)));
        }

        [TestCase(VoteKickOutcome.Passed)]
        [TestCase(VoteKickOutcome.Failed)]
        public void TheDecisionClearsTheShownVoteButKeepsTheSender(VoteKickOutcome outcome)
        {
            _part.Apply(new SnapshotMessage());
            _part.Handle(State(4, 2, 1, 2, 25f));

            Assert.That(_part.Handle(State(4, 2, 2, 2, 0f, outcome)), Is.True);

            Assert.That(GameSignals.VoteKick.HasValue, Is.False);
            Assert.That(GameSignals.VoteKickSender.Value, Is.SameAs(_part.VoteKickSender), "another vote can be called later");
            Assert.That(_logs, Has.Member(VoteKickLog.Decided(4, outcome)));
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(VoteKickLog.Changed(null)));
        }

        [Test]
        public void ClearingDoesNotRemoveAVoteSomeoneElsePublished()
        {
            _part.Handle(State(4, 2, 1, 2, 25f));
            var other = new VoteKickStatus(9, 1, 0, 2, 30f);
            GameSignals.VoteKick.Publish(other);

            _part.Handle(State(4, 2, 0, 2, 0f, VoteKickOutcome.Failed));

            Assert.That(GameSignals.VoteKick.Value, Is.EqualTo((VoteKickStatus?)other));
        }

        [Test]
        public void OtherMessagesAreLeftToTheRest()
        {
            Assert.That(_part.Handle(new VoteKickRequestMessage()), Is.False);
            Assert.That(_part.Handle(new MapVoteStateMessage()), Is.False);
            Assert.That(GameSignals.VoteKick.HasValue, Is.False);
        }

        [Test]
        public void CallingThroughTheSenderSendsAVoteInFavorOfTheTarget()
        {
            _part.Apply(new SnapshotMessage());

            GameSignals.VoteKickSender.Value.Call(6);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].TargetPlayerId, Is.EqualTo(6));
            Assert.That(_sent[0].InFavor, Is.True);
            Assert.That(_logs, Has.Member(VoteKickLog.Called(6, true)));
        }

        [Test]
        public void VotingThroughTheSenderSendsTheChoiceOnTheVoteInProgress()
        {
            _part.Apply(new SnapshotMessage());
            _part.Handle(State(4, 2, 1, 3, 25f));

            GameSignals.VoteKickSender.Value.Vote(false);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].TargetPlayerId, Is.EqualTo(4));
            Assert.That(_sent[0].InFavor, Is.False);
            Assert.That(_logs, Has.Member(VoteKickLog.Voted(4, false, true)));
        }

        [Test]
        public void VotingWithNoVoteInProgressSendsNothing()
        {
            _part.Apply(new SnapshotMessage());

            _part.VoteKickSender.Vote(true);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(VoteKickLog.NoVote(true)));
        }

        [Test]
        public void ARequestThatCouldNotBeSentIsLogged()
        {
            _part.Handle(State(4, 2, 1, 3, 25f));
            _connected = false;

            _part.VoteKickSender.Call(7);
            _part.VoteKickSender.Vote(true);

            Assert.That(_logs, Has.Member(VoteKickLog.Called(7, false)));
            Assert.That(_logs, Has.Member(VoteKickLog.Voted(4, true, false)));
        }

        [Test]
        public void DisposeClearsWhatThisPartPublishedAndStopsSending()
        {
            _part.Apply(new SnapshotMessage());
            _part.Handle(State(4, 2, 1, 3, 25f));
            var sender = GameSignals.VoteKickSender.Value;

            _part.Dispose();

            Assert.That(GameSignals.VoteKick.HasValue, Is.False);
            Assert.That(GameSignals.VoteKickSender.HasValue, Is.False);
            sender.Call(5);
            sender.Vote(true);
            Assert.That(_sent, Is.Empty, "a sender kept after leaving does not send");
            Assert.That(_logs, Has.Member(VoteKickLog.CallAfterLeaving(5)));
            Assert.That(_logs, Has.Member(VoteKickLog.VoteAfterLeaving(true)));
        }

        [Test]
        public void AStateAfterDisposeIsTakenButNotShown()
        {
            _part.Dispose();

            Assert.That(_part.Handle(State(4, 2, 1, 3, 25f)), Is.True);

            Assert.That(GameSignals.VoteKick.HasValue, Is.False);
        }

        [Test]
        public void DisposeLeavesASenderSomeoneElsePublished()
        {
            _part.Apply(new SnapshotMessage());
            var other = new NetV2VoteKickPart(Send, _logs.Add);
            GameSignals.VoteKickSender.Publish(other.VoteKickSender);

            _part.Dispose();

            Assert.That(GameSignals.VoteKickSender.Value, Is.SameAs(other.VoteKickSender));
            other.Dispose();
        }

        private static VoteKickStateMessage State(
            int target, int caller, int yes, int required, float seconds, VoteKickOutcome outcome = VoteKickOutcome.Undecided)
        {
            return new VoteKickStateMessage
            {
                TargetPlayerId = target,
                CallerPlayerId = caller,
                YesVotes = yes,
                RequiredVotes = required,
                RemainingSeconds = seconds,
                Outcome = outcome,
            };
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            _sent.Add((VoteKickRequestMessage)message);
            return true;
        }

        private void CountChange(VoteKickStatus? status)
        {
            _changes++;
        }
    }
}
