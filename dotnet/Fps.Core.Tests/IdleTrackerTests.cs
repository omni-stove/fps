using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class IdleTrackerTests
    {
        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Ids(params int[] expected) => expected;

        private static IdleTracker Create(float kickSeconds = 30f, float warningSeconds = 15f)
        {
            var tracker = new IdleTracker(kickSeconds, warningSeconds);
            tracker.Add(1);
            return tracker;
        }

        [Test]
        public void TheDefaultWarningIsFifteenSeconds()
        {
            Assert.That(IdleTracker.DefaultWarningSeconds, Is.EqualTo(15f));
            Assert.That(new IdleTracker(120f).WarningSeconds, Is.EqualTo(15f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidSeconds(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdleTracker(seconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdleTracker(30f, seconds));
        }

        [Test]
        public void APlayerStartsWithTheFullTimeAndCounting()
        {
            var tracker = Create();

            Assert.That(tracker.Count, Is.EqualTo(1));
            Assert.That(tracker.Contains(1), Is.True);
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(30f));
            Assert.That(tracker.IsCounting(1), Is.True);
            Assert.That(tracker.IsWarned(1), Is.False);
            Assert.That(tracker.IsKicked(1), Is.False);
        }

        [Test]
        public void AddingTheSamePlayerTwiceIsRejected()
        {
            var tracker = Create();

            Assert.Throws<ArgumentException>(() => tracker.Add(1));
        }

        [Test]
        public void WarnsOnceWhenFifteenSecondsAreLeftAndKicksAtZero()
        {
            var tracker = Create();

            Assert.That(tracker.Advance(1, 14f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 1f), Is.EqualTo(IdleChange.Warned));
            Assert.That(tracker.IsWarned(1), Is.True);
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(15f));
            Assert.That(tracker.Advance(1, 10f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 5f), Is.EqualTo(IdleChange.Kicked));
            Assert.That(tracker.IsKicked(1), Is.True);
            Assert.That(tracker.IsWarned(1), Is.False);
            Assert.That(tracker.IsCounting(1), Is.False);
            Assert.That(tracker.GetRemaining(1), Is.Zero);
        }

        [Test]
        public void AKickIsReportedOnlyOnce()
        {
            var tracker = Create();

            Assert.That(tracker.Advance(1, 40f), Is.EqualTo(IdleChange.Kicked));
            Assert.That(tracker.Advance(1, 1f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Touch(1), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.SetCounting(1, false), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.SetCounting(1, true), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.IsKicked(1), Is.True);
        }

        [Test]
        public void InputPutsTheClockBackAndClearsTheWarning()
        {
            var tracker = Create();
            tracker.Advance(1, 20f);

            Assert.That(tracker.Touch(1), Is.EqualTo(IdleChange.WarningCleared));
            Assert.That(tracker.IsWarned(1), Is.False);
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(30f));

            Assert.That(tracker.Advance(1, 14f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 1f), Is.EqualTo(IdleChange.Warned));
        }

        [Test]
        public void InputWithoutAWarningOnlyPutsTheClockBack()
        {
            var tracker = Create();
            tracker.Advance(1, 10f);

            Assert.That(tracker.Touch(1), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(30f));
        }

        [Test]
        public void NotCountingStopsTheClockWithoutPuttingItBack()
        {
            var tracker = Create();
            tracker.Advance(1, 10f);

            Assert.That(tracker.SetCounting(1, false), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.IsCounting(1), Is.False);
            Assert.That(tracker.Advance(1, 100f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(20f));

            Assert.That(tracker.SetCounting(1, true), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 5f), Is.EqualTo(IdleChange.Warned));
        }

        [Test]
        public void StoppingWhileWarnedClearsTheWarningAndResumingWarnsAgain()
        {
            var tracker = Create();
            tracker.Advance(1, 20f);

            Assert.That(tracker.SetCounting(1, false), Is.EqualTo(IdleChange.WarningCleared));
            Assert.That(tracker.IsWarned(1), Is.False);
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(10f));

            Assert.That(tracker.SetCounting(1, true), Is.EqualTo(IdleChange.Warned));
            Assert.That(tracker.IsWarned(1), Is.True);
            Assert.That(tracker.Advance(1, 9f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 1f), Is.EqualTo(IdleChange.Kicked));
        }

        [Test]
        public void SettingTheSameCountingStateChangesNothing()
        {
            var tracker = Create();
            tracker.Advance(1, 20f);

            Assert.That(tracker.SetCounting(1, true), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.IsWarned(1), Is.True);
            tracker.SetCounting(1, false);
            Assert.That(tracker.SetCounting(1, false), Is.EqualTo(IdleChange.None));
        }

        [Test]
        public void InputWhileNotCountingPutsTheClockBack()
        {
            var tracker = Create();
            tracker.Advance(1, 20f);
            tracker.SetCounting(1, false);

            Assert.That(tracker.Touch(1), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(30f));
            Assert.That(tracker.SetCounting(1, true), Is.EqualTo(IdleChange.None));
        }

        [Test]
        public void AKickTimeShorterThanTheWarningWarnsOnTheFirstAdvance()
        {
            var tracker = Create(kickSeconds: 10f);

            Assert.That(tracker.Advance(1, 0f), Is.EqualTo(IdleChange.Warned));
            Assert.That(tracker.Advance(1, 9f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Advance(1, 1f), Is.EqualTo(IdleChange.Kicked));
        }

        [Test]
        public void PlayersAreCountedSeparately()
        {
            var tracker = Create();
            tracker.Add(2);

            tracker.Advance(1, 20f);
            tracker.Advance(2, 5f);

            Assert.That(tracker.IsWarned(1), Is.True);
            Assert.That(tracker.IsWarned(2), Is.False);
            Assert.That(tracker.GetRemaining(2), Is.EqualTo(25f));
            Assert.That(tracker.PlayerIds, Is.EquivalentTo(Ids(1, 2)));
        }

        [Test]
        public void RemovedOrUnknownPlayersAreIgnored()
        {
            var tracker = Create();

            Assert.That(tracker.Remove(1), Is.True);
            Assert.That(tracker.Remove(1), Is.False);
            Assert.That(tracker.Count, Is.Zero);
            Assert.That(tracker.Advance(1, 100f), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.Touch(1), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.SetCounting(1, false), Is.EqualTo(IdleChange.None));
            Assert.That(tracker.GetRemaining(1), Is.Null);
            Assert.That(tracker.IsCounting(1), Is.False);
            Assert.That(tracker.IsKicked(1), Is.False);
        }

        [Test]
        public void ARemovedPlayerCanBeAddedAgainWithTheFullTime()
        {
            var tracker = Create();
            tracker.Advance(1, 40f);
            tracker.Remove(1);

            tracker.Add(1);

            Assert.That(tracker.IsKicked(1), Is.False);
            Assert.That(tracker.GetRemaining(1), Is.EqualTo(30f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AdvanceRejectsInvalidSeconds(float deltaSeconds)
        {
            var tracker = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Advance(1, deltaSeconds));
        }
    }
}
