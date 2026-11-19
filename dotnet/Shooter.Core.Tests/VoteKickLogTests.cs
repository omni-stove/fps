using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VoteKickLogTests
    {
        [Test]
        public void SettingsSayOffWhenDisabled()
        {
            Assert.That(VoteKickLog.Settings(VoteKickSpec.Default), Is.EqualTo("[VoteKick] off"));
        }

        [Test]
        public void SettingsListTheFractionAndSecondsWhenEnabled()
        {
            Assert.That(
                VoteKickLog.Settings(new VoteKickSpec(true, 0.6f, 30f, 120f)),
                Is.EqualTo("[VoteKick] on yesFraction=0.6 seconds=30.0 cooldown=120.0"));
        }

        [Test]
        public void CalledNamesTheCallerTargetAndResult()
        {
            Assert.That(VoteKickLog.Called(1, 3, VoteKickCall.Disabled), Is.EqualTo("[VoteKick] call caller=1 target=3 result=Disabled"));
        }

        [Test]
        public void OpenedListsTheVotersAndTheRequiredVotes()
        {
            var vote = new VoteKick();
            vote.Call(new VoteKickSpec(true, 0.6f, 30f, 120f), 1, "a", 4, Players(1, 2, 3, 4));

            Assert.That(VoteKickLog.Opened(vote), Is.EqualTo("[VoteKick] opened target=4 caller=1 voters=3 required=2 seconds=30.0"));
        }

        [Test]
        public void VotedSaysWhichWay()
        {
            Assert.That(
                VoteKickLog.Voted(2, 4, false, VoteKickBallot.Cast),
                Is.EqualTo("[VoteKick] vote player=2 target=4 inFavor=false result=Cast"));
        }

        [Test]
        public void DecidedListsTheCountsAndTheEnd()
        {
            var result = new VoteKickResult(4, 1, 2, 1, 2, VoteKickEnd.Passed);

            Assert.That(VoteKickLog.Decided(result), Is.EqualTo("[VoteKick] decided target=4 caller=1 yes=2 no=1 required=2 end=Passed"));
            Assert.That(VoteKickLog.KickedLogText(result), Is.EqualTo("vote kicked (yes 2/2)"));
        }

        [Test]
        public void KickedSaysWhetherARejoinIsBarred()
        {
            Assert.That(VoteKickLog.Kicked(4, true), Is.EqualTo("[VoteKick] kicked player=4 barred until the next match"));
            Assert.That(VoteKickLog.Kicked(4, false), Is.EqualTo("[VoteKick] kicked player=4 (no rejoin key; cannot bar a rejoin)"));
        }

        [Test]
        public void RefusedRejoinNamesTheMatch()
        {
            Assert.That(VoteKickLog.RefusedRejoin("m-1"), Is.EqualTo("vote kicked from match m-1; refused until the next match"));
            Assert.That(VoteKickLog.RefusedRejoin(null), Is.EqualTo("vote kicked from match none; refused until the next match"));
        }

        [Test]
        public void LeftAndUnbarredCountThePlayers()
        {
            Assert.That(VoteKickLog.Left(2), Is.EqualTo("[VoteKick] left player=2"));
            Assert.That(VoteKickLog.Unbarred(1), Is.EqualTo("[VoteKick] next match; 1 vote-kicked account(s) may join again"));
        }

        [Test]
        public void NullArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => VoteKickLog.Settings(null));
            Assert.Throws<ArgumentNullException>(() => VoteKickLog.Opened(null));
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Players(params int[] ids) => ids;
    }
}
