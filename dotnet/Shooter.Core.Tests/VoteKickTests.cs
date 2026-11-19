using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VoteKickTests
    {
        private static readonly VoteKickSpec _spec = new VoteKickSpec(true, 0.6f, 30f, 120f);

        private VoteKick _vote;

        [SetUp]
        public void SetUp()
        {
            _vote = new VoteKick();
        }

        [TestCase(2, 0.6f, 2)]
        [TestCase(3, 0.6f, 2)]
        [TestCase(5, 0.6f, 3)]
        [TestCase(4, 0.5f, 2)]
        [TestCase(3, 0.5f, 2)]
        [TestCase(10, 0.7f, 7)]
        [TestCase(10, 0.75f, 8)]
        [TestCase(3, 1f, 3)]
        [TestCase(7, 0.1f, 1)]
        [TestCase(1, 0.6f, 1)]
        [TestCase(0, 0.6f, 1)]
        public void TheRequiredVotesAreTheVotersTimesTheFractionRoundedUp(int voters, float fraction, int expected)
        {
            Assert.That(VoteKick.RequiredFor(voters, fraction), Is.EqualTo(expected));
        }

        [Test]
        public void RequiredForRefusesBadInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VoteKick.RequiredFor(-1, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoteKick.RequiredFor(3, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoteKick.RequiredFor(3, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoteKick.RequiredFor(3, float.NaN));
        }

        [Test]
        public void TwoOfThreePlayersVotingOutTheThirdPasses()
        {
            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.Started));

            Assert.That(_vote.IsOpen, Is.True);
            Assert.That(_vote.VoterCount, Is.EqualTo(2), "the target does not vote");
            Assert.That(_vote.Status, Is.EqualTo((VoteKickStatus?)new VoteKickStatus(3, 1, 1, 2, 30f)));
            Assert.That(_vote.HasVoted(1), Is.True, "calling is a vote in favour");

            Assert.That(_vote.Vote(2, 3, true), Is.EqualTo(VoteKickBallot.Cast));

            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.Status, Is.Null);
            var result = _vote.Result.Value;
            Assert.That(result.End, Is.EqualTo(VoteKickEnd.Passed));
            Assert.That(result.Passed, Is.True);
            Assert.That(result.TargetId, Is.EqualTo(3));
            Assert.That(result.CallerId, Is.EqualTo(1));
            Assert.That(result.YesVotes, Is.EqualTo(2));
            Assert.That(result.NoVotes, Is.EqualTo(0));
            Assert.That(result.RequiredVotes, Is.EqualTo(2));
        }

        [Test]
        public void TheFractionDecidesHowManyMustAgree()
        {
            var strict = new VoteKickSpec(true, 1f, 30f, 0f);
            _vote.Call(strict, 1, "a", 5, Players(1, 2, 3, 4, 5));
            Assert.That(_vote.RequiredVotes, Is.EqualTo(4));

            _vote.Vote(2, 5, true);
            _vote.Vote(3, 5, true);
            Assert.That(_vote.IsOpen, Is.True, "3 of 4 is not every voter");

            _vote.Vote(4, 5, true);
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Passed));
        }

        [Test]
        public void TheVoteFailsWhenTheTimeRunsOut()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Tick(29.5f), Is.False);
            Assert.That(_vote.IsOpen, Is.True);
            Assert.That(_vote.RemainingSeconds, Is.EqualTo(0.5f).Within(1e-4f));

            Assert.That(_vote.Tick(0.5f), Is.True);

            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.RemainingSeconds, Is.Zero);
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Expired));
            Assert.That(_vote.Result.Value.YesVotes, Is.EqualTo(1));
            Assert.That(_vote.Tick(1f), Is.False, "a closed vote does not close again");
            Assert.That(_vote.Vote(2, 4, true), Is.EqualTo(VoteKickBallot.NoVote));
        }

        [Test]
        public void EnoughVotesAgainstEndTheVoteBeforeTheDeadline()
        {
            // 3 人が票を持ち、要るのは 2 票
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Vote(2, 4, false), Is.EqualTo(VoteKickBallot.Cast));
            Assert.That(_vote.IsOpen, Is.True, "the last voter can still pass it");

            Assert.That(_vote.Vote(3, 4, false), Is.EqualTo(VoteKickBallot.Cast));

            Assert.That(_vote.IsOpen, Is.False);
            var result = _vote.Result.Value;
            Assert.That(result.End, Is.EqualTo(VoteKickEnd.Defeated));
            Assert.That(result.YesVotes, Is.EqualTo(1));
            Assert.That(result.NoVotes, Is.EqualTo(2));
        }

        [Test]
        public void AVoterCanChangeTheirVote()
        {
            _vote.Call(_spec, 1, "a", 5, Players(1, 2, 3, 4, 5));

            Assert.That(_vote.Vote(2, 5, false), Is.EqualTo(VoteKickBallot.Cast));
            Assert.That(_vote.Vote(2, 5, false), Is.EqualTo(VoteKickBallot.Unchanged));
            Assert.That(_vote.NoVotes, Is.EqualTo(1));

            Assert.That(_vote.Vote(2, 5, true), Is.EqualTo(VoteKickBallot.Changed));
            Assert.That(_vote.YesVotes, Is.EqualTo(2));
            Assert.That(_vote.NoVotes, Is.EqualTo(0));
            Assert.That(_vote.IsOpen, Is.True, "4 voters need 3 votes");
        }

        [Test]
        public void ALeaversVoteIsDroppedAndTheRequiredVotesAreCountedAgain()
        {
            // 4 人が票を持ち、要るのは 3 票
            _vote.Call(_spec, 1, "a", 5, Players(1, 2, 3, 4, 5));
            _vote.Vote(2, 5, true);
            Assert.That(_vote.RequiredVotes, Is.EqualTo(3));

            Assert.That(_vote.Leave(2), Is.True);

            Assert.That(_vote.VoterCount, Is.EqualTo(3));
            Assert.That(_vote.YesVotes, Is.EqualTo(1), "the leaver's vote is gone");
            Assert.That(_vote.RequiredVotes, Is.EqualTo(2));
            Assert.That(_vote.IsOpen, Is.True);

            _vote.Vote(3, 5, true);
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Passed));
            Assert.That(_vote.Result.Value.RequiredVotes, Is.EqualTo(2));
        }

        [Test]
        public void SomeoneLeavingCanBringTheVotesInFavourUpToTheRequiredNumber()
        {
            _vote.Call(_spec, 1, "a", 5, Players(1, 2, 3, 4, 5));
            _vote.Vote(2, 5, true);
            _vote.Vote(3, 5, false);
            Assert.That(_vote.IsOpen, Is.True);

            Assert.That(_vote.Leave(4), Is.True);

            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Passed));
            Assert.That(_vote.Result.Value.YesVotes, Is.EqualTo(2));
        }

        [Test]
        public void TheTargetLeavingEndsTheVoteWithoutPassingIt()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Leave(4), Is.True);

            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.TargetLeft));
            Assert.That(_vote.Result.Value.Passed, Is.False);
        }

        [Test]
        public void SomeoneWhoHadNoVoteLeavingChangesNothing()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Leave(9), Is.False);
            Assert.That(_vote.VoterCount, Is.EqualTo(3));
            Assert.That(new VoteKick().Leave(1), Is.False, "nothing to leave without a vote");
        }

        [Test]
        public void TheSameCallerCannotCallAgainUntilTheCooldownHasPassed()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));
            _vote.Cancel();
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Cancelled));

            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.CoolingDown));
            Assert.That(_vote.CooldownRemaining(1, "a"), Is.EqualTo(120f));

            _vote.Tick(119f);
            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.CoolingDown));
            Assert.That(_vote.CooldownRemaining(1, "a"), Is.EqualTo(1f).Within(1e-3f));

            _vote.Tick(1f);
            Assert.That(_vote.CooldownRemaining(1, "a"), Is.Zero);
            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.Started));
        }

        [Test]
        public void TheCooldownBelongsToTheCallerAndNotToEveryone()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));
            _vote.Cancel();

            Assert.That(_vote.Call(_spec, 2, "b", 4, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.Started));
        }

        [Test]
        public void ReconnectingWithTheSameKeyDoesNotSkipTheCooldown()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));
            _vote.Cancel();

            Assert.That(_vote.Call(_spec, 7, "a", 4, Players(7, 2, 3, 4)), Is.EqualTo(VoteKickCall.CoolingDown));
        }

        [Test]
        public void WithoutAKeyTheCooldownFollowsThePlayerId()
        {
            _vote.Call(_spec, 1, null, 4, Players(1, 2, 3, 4));
            _vote.Cancel();

            Assert.That(_vote.Call(_spec, 1, string.Empty, 4, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.CoolingDown));
            Assert.That(_vote.Call(_spec, 2, "1", 4, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.Started), "a key that reads like an id is someone else");
        }

        [Test]
        public void ZeroCooldownSecondsLetTheCallerCallAgainAtOnce()
        {
            var spec = new VoteKickSpec(true, 0.6f, 30f, 0f);
            _vote.Call(spec, 1, "a", 4, Players(1, 2, 3, 4));
            _vote.Cancel();

            Assert.That(_vote.Call(spec, 1, "a", 4, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.Started));
        }

        [Test]
        public void ARefusedCallStartsNoCooldown()
        {
            _vote.Call(_spec, 1, "a", 1, Players(1, 2, 3));

            Assert.That(_vote.CooldownRemaining(1, "a"), Is.Zero);
            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.Started));
        }

        [Test]
        public void ADisabledSpecRefusesEveryCall()
        {
            Assert.That(_vote.Call(VoteKickSpec.Default, 1, "a", 3, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.Disabled));
            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.Result, Is.Null);
        }

        [Test]
        public void NobodyCanVoteThemselvesOut()
        {
            Assert.That(_vote.Call(_spec, 2, "b", 2, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.Self));
            Assert.That(_vote.IsOpen, Is.False);
        }

        [Test]
        public void BotsCannotBeVotedOutAndHaveNoVote()
        {
            var bot = BotRoster.FirstBotPlayerId;

            Assert.That(_vote.Call(_spec, 1, "a", bot, Players(1, 2, bot)), Is.EqualTo(VoteKickCall.Bot));

            Assert.That(_vote.Call(_spec, 1, "a", 3, Players(1, 2, 3, bot, bot + 1)), Is.EqualTo(VoteKickCall.Started));
            Assert.That(_vote.VoterCount, Is.EqualTo(2));
            Assert.That(_vote.Vote(bot, 3, true), Is.EqualTo(VoteKickBallot.NotVoter));
        }

        [Test]
        public void OnlyOneVoteRunsAtATime()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Call(_spec, 2, "b", 3, Players(1, 2, 3, 4)), Is.EqualTo(VoteKickCall.InProgress));
            Assert.That(_vote.TargetId, Is.EqualTo(4));
            Assert.That(_vote.CooldownRemaining(2, "b"), Is.Zero);
        }

        [Test]
        public void TheTargetAndTheCallerMustBeInTheMatch()
        {
            Assert.That(_vote.Call(_spec, 1, "a", 9, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.TargetNotPresent));
            Assert.That(_vote.Call(_spec, 9, "z", 3, Players(1, 2, 3)), Is.EqualTo(VoteKickCall.CallerCannotVote));
            Assert.That(_vote.IsOpen, Is.False);
        }

        [Test]
        public void OnlyThoseInTheMatchWhenItWasCalledCanVote()
        {
            _vote.Call(_spec, 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(_vote.Vote(9, 4, true), Is.EqualTo(VoteKickBallot.NotVoter), "joined after the call");
            Assert.That(_vote.Vote(4, 4, false), Is.EqualTo(VoteKickBallot.NotVoter), "the target has no vote");
            Assert.That(_vote.Vote(2, 3, true), Is.EqualTo(VoteKickBallot.OtherTarget));
            Assert.That(_vote.CanVote(2), Is.True);
            Assert.That(_vote.CanVote(9), Is.False);
            Assert.That(_vote.YesVotes, Is.EqualTo(1));
        }

        [Test]
        public void AVoteWithoutAnOpenVoteIsRefused()
        {
            Assert.That(_vote.Vote(1, 2, true), Is.EqualTo(VoteKickBallot.NoVote));
        }

        [Test]
        public void ACallerAloneWithTheTargetPassesAtOnce()
        {
            Assert.That(_vote.Call(_spec, 1, "a", 2, Players(1, 2)), Is.EqualTo(VoteKickCall.Started));

            Assert.That(_vote.IsOpen, Is.False);
            Assert.That(_vote.Result.Value.End, Is.EqualTo(VoteKickEnd.Passed));
            Assert.That(_vote.Result.Value.RequiredVotes, Is.EqualTo(1));
        }

        [Test]
        public void ANewCallForgetsThePreviousResult()
        {
            var spec = new VoteKickSpec(true, 0.6f, 30f, 0f);
            _vote.Call(spec, 1, "a", 4, Players(1, 2, 3, 4));
            _vote.Cancel();
            Assert.That(_vote.Result, Is.Not.Null);

            _vote.Call(spec, 2, "b", 3, Players(1, 2, 3, 4));

            Assert.That(_vote.Result, Is.Null);
            Assert.That(_vote.YesVotes, Is.EqualTo(1));
            Assert.That(_vote.HasVoted(1), Is.False, "the earlier caller's vote was for someone else");
        }

        [Test]
        public void CancelWithoutAVoteDoesNothing()
        {
            Assert.That(_vote.Cancel(), Is.False);
            Assert.That(_vote.Result, Is.Null);
        }

        [Test]
        public void BadArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _vote.Call(null, 1, "a", 2, Players(1, 2)));
            Assert.Throws<ArgumentNullException>(() => _vote.Call(_spec, 1, "a", 2, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vote.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vote.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vote.Tick(float.PositiveInfinity));
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Players(params int[] ids) => ids;
    }
}
