using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StreakTrackerTests
    {
        private static readonly string[] _uavOnly = { "uav" };
        private static readonly string[] _airstrikeOnly = { "airstrike" };
        private static readonly string[] _uavThenAirstrike = { "uav", "airstrike" };
        private static readonly string[] _airstrikeThenUav = { "airstrike", "uav" };

        [Test]
        public void ANewTrackerHasNoKillsAndListsTheRewardsInOrder()
        {
            var tracker = new StreakTracker(StreakSpec.Default);

            Assert.That(tracker.Kills, Is.EqualTo(0));
            Assert.That(tracker.Status.Kills, Is.EqualTo(0));
            Assert.That(tracker.Status.ReadyCount, Is.EqualTo(0));
            Assert.That(tracker.Status.Slots, Is.EqualTo(new[]
            {
                new StreakSlot("uav", "streak.uav", 3, false),
                new StreakSlot("airstrike", "streak.airstrike", 5, false),
            }));
            Assert.That(tracker.ReadyRewardIds, Is.Empty);
        }

        [Test]
        public void ARewardIsEarnedWhenTheKillsReachIt()
        {
            var tracker = new StreakTracker(StreakSpec.Default);

            tracker.Killed(out var first);
            tracker.Killed(out var second);
            var status = tracker.Killed(out var third);

            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(third, Is.EqualTo(_uavOnly));
            Assert.That(status.Kills, Is.EqualTo(3));
            Assert.That(status.ReadyCount, Is.EqualTo(1));
            Assert.That(status.Slots[0], Is.EqualTo(new StreakSlot("uav", "streak.uav", 3, true)));
            Assert.That(status.Slots[1].Ready, Is.False);
            Assert.That(tracker.Status, Is.EqualTo(status));

            tracker.Killed();
            var fifth = tracker.Killed(out var atFive);

            Assert.That(atFive, Is.EqualTo(_airstrikeOnly));
            Assert.That(fifth.Kills, Is.EqualTo(5));
            Assert.That(fifth.ReadyCount, Is.EqualTo(2));
            Assert.That(tracker.ReadyRewardIds, Is.EqualTo(_uavThenAirstrike));
        }

        [Test]
        public void DyingResetsTheKillsButKeepsTheEarnedRewards()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 4);

            var status = tracker.Died();

            Assert.That(status.Kills, Is.EqualTo(0));
            Assert.That(tracker.Kills, Is.EqualTo(0));
            Assert.That(status.ReadyCount, Is.EqualTo(1));
            Assert.That(status.Slots[0].Ready, Is.True);
            Assert.That(tracker.ReadyRewardIds, Is.EqualTo(_uavOnly));

            tracker.Killed();
            Assert.That(tracker.Kills, Is.EqualTo(1), "counts again from zero");
            Assert.That(tracker.Status.Slots[1].Ready, Is.False, "the airstrike needs five kills in the new life");
        }

        [Test]
        public void ARewardIsEarnedOnlyOncePerLife()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 3);
            tracker.Use(out var used);
            Assert.That(used, Is.EqualTo("uav"));

            tracker.Killed(out var atFour);
            tracker.Killed(out var atFive);
            tracker.Killed(out var atSix);

            Assert.That(atFour, Is.Empty);
            Assert.That(atFive, Is.EqualTo(_airstrikeOnly));
            Assert.That(atSix, Is.Empty);
            Assert.That(tracker.ReadyRewardIds, Is.EqualTo(_airstrikeOnly), "the used UAV does not come back in the same life");
        }

        [Test]
        public void AUsedRewardCanBeEarnedAgainInTheNextLife()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 3);
            tracker.Use(out _);
            tracker.Died();

            tracker.Killed();
            tracker.Killed();
            tracker.Killed(out var earned);

            Assert.That(earned, Is.EqualTo(_uavOnly));
            Assert.That(tracker.Status.ReadyCount, Is.EqualTo(1));
        }

        [Test]
        public void AHeldRewardIsNotStackedWhenEarnedAgain()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 3);
            tracker.Died();

            tracker.Killed();
            tracker.Killed();
            var status = tracker.Killed(out var earned);

            Assert.That(earned, Is.Empty);
            Assert.That(status.ReadyCount, Is.EqualTo(1));
            tracker.Use(out var first);
            tracker.Use(out var second);
            Assert.That(first, Is.EqualTo("uav"));
            Assert.That(second, Is.Null);
        }

        [Test]
        public void RewardsAreUsedInTheOrderTheyWereEarned()
        {
            var spec = new StreakSpec(new[]
            {
                new StreakReward("uav", "streak.uav", 4),
                new StreakReward("airstrike", "streak.airstrike", 2),
            });
            var tracker = new StreakTracker(spec);
            KillTimes(tracker, 4);

            var afterFirst = tracker.Use(out var first);
            var afterSecond = tracker.Use(out var second);

            Assert.That(first, Is.EqualTo("airstrike"), "earned at two kills");
            Assert.That(second, Is.EqualTo("uav"));
            Assert.That(afterFirst.ReadyCount, Is.EqualTo(1));
            Assert.That(afterFirst.Slots[0].Ready, Is.True);
            Assert.That(afterFirst.Slots[1].Ready, Is.False);
            Assert.That(afterSecond.ReadyCount, Is.EqualTo(0));
            Assert.That(afterSecond.Kills, Is.EqualTo(4), "using does not change the kills");
        }

        [Test]
        public void RewardsReachedOnTheSameKillAreEarnedInListOrder()
        {
            var spec = new StreakSpec(new[]
            {
                new StreakReward("airstrike", "streak.airstrike", 2),
                new StreakReward("uav", "streak.uav", 2),
            });
            var tracker = new StreakTracker(spec);

            tracker.Killed();
            tracker.Killed(out var earned);

            Assert.That(earned, Is.EqualTo(_airstrikeThenUav));
            Assert.That(tracker.ReadyRewardIds, Is.EqualTo(_airstrikeThenUav));
        }

        [Test]
        public void UsingWithNothingReadyGivesNoRewardAndKeepsTheStatus()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            tracker.Killed();
            var before = tracker.Status;

            var status = tracker.Use(out var rewardId);

            Assert.That(rewardId, Is.Null);
            Assert.That(status, Is.EqualTo(before));
        }

        [Test]
        public void ResetDropsTheKillsAndTheEarnedRewards()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 5);

            var status = tracker.Reset();

            Assert.That(status.Kills, Is.EqualTo(0));
            Assert.That(status.ReadyCount, Is.EqualTo(0));
            Assert.That(tracker.ReadyRewardIds, Is.Empty);
            KillTimes(tracker, 3);
            Assert.That(tracker.ReadyRewardIds, Is.EqualTo(_uavOnly), "a reset starts a new life");
        }

        [Test]
        public void WithoutRewardsTheKillsStillCount()
        {
            var tracker = new StreakTracker(new StreakSpec(Array.Empty<StreakReward>()));

            var status = tracker.Killed(out var earned);

            Assert.That(earned, Is.Empty);
            Assert.That(status.Kills, Is.EqualTo(1));
            Assert.That(status.Slots, Is.Empty);
        }

        [Test]
        public void ANullSpecIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new StreakTracker(null));
        }

        [Test]
        public void TheLogLinesCarryTheTagAndTheCounts()
        {
            var tracker = new StreakTracker(StreakSpec.Default);
            KillTimes(tracker, 3);
            var status = tracker.Status;

            Assert.That(StreakLog.Configured("Player", StreakSpec.Default), Is.EqualTo("[Streak] id=Player rewards=uav@3,airstrike@5"));
            Assert.That(
                StreakLog.Configured("Player", new StreakSpec(Array.Empty<StreakReward>())), Is.EqualTo("[Streak] id=Player rewards=none"));
            Assert.That(StreakLog.Killed("Player", status), Is.EqualTo("[Streak] id=Player kill kills=3 ready=1"));
            Assert.That(StreakLog.Earned("Player", "uav", status), Is.EqualTo("[Streak] id=Player earned reward=uav kills=3 ready=1"));
            Assert.That(StreakLog.Died("Player", 3, tracker.Died()), Is.EqualTo("[Streak] id=Player died lost=3 kills=0 ready=1"));
            Assert.That(StreakLog.Used("Player", "uav", tracker.Use(out _)), Is.EqualTo("[Streak] id=Player used reward=uav kills=0 ready=0"));
            Assert.That(StreakLog.NothingToUse("Player"), Is.EqualTo("[Streak] id=Player use refused nothing-ready"));
            Assert.That(StreakLog.UseRefusedDead("Player"), Is.EqualTo("[Streak] id=Player use refused dead"));
            Assert.That(
                StreakLog.RewardUnavailable("Player", "uav", "PingController"),
                Is.EqualTo("[Streak] id=Player use refused reward=uav missing=PingController"));
        }

        private static void KillTimes(StreakTracker tracker, int count)
        {
            for (var i = 0; i < count; i++)
            {
                tracker.Killed();
            }
        }
    }
}
