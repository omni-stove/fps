using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CoopHoldTests
    {
        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopHoldSpec(0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopHoldSpec(2, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopHoldSpec(2, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopHoldSpec(2, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new CoopHold(null));
        }

        [Test]
        public void TheDefaultSpecNeedsTwoPeople()
        {
            Assert.That(CoopHoldSpec.Default.RequiredCount, Is.EqualTo(2));
            Assert.That(CoopHoldSpec.Default.HoldSeconds, Is.EqualTo(CoopHoldSpec.DefaultHoldSeconds));
            Assert.That(CoopHoldSpec.Default.ResetOnInterrupt, Is.False);
        }

        [Test]
        public void TickRejectsInvalidArguments()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f));

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.Tick(-1, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.Tick(2, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.Tick(2, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.Tick(2, float.PositiveInfinity));
        }

        [Test]
        public void OnePersonAloneDoesNotProgress()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f));

            Assert.That(hold.Tick(1, 5f), Is.EqualTo(CoopHoldStep.None));
            Assert.That(hold.HoldingCount, Is.EqualTo(1));
            Assert.That(hold.ElapsedSeconds, Is.Zero);
            Assert.That(hold.IsRunning, Is.False);
            Assert.That(hold.IsCompleted, Is.False);
        }

        [Test]
        public void EnoughPeopleProgressUntilTheHoldSecondsAndComplete()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f));

            Assert.That(hold.Tick(2, 0.25f), Is.EqualTo(CoopHoldStep.Started));
            Assert.That(hold.IsRunning, Is.True);
            Assert.That(hold.Progress, Is.EqualTo(0.25f));

            Assert.That(hold.Tick(3, 0.5f), Is.EqualTo(CoopHoldStep.None), "more than the required count also progresses");
            Assert.That(hold.ElapsedSeconds, Is.EqualTo(0.75f));

            Assert.That(hold.Tick(2, 0.5f), Is.EqualTo(CoopHoldStep.Completed));
            Assert.That(hold.IsCompleted, Is.True);
            Assert.That(hold.IsRunning, Is.False);
            Assert.That(hold.ElapsedSeconds, Is.EqualTo(1f), "elapsed stops at the hold seconds");
            Assert.That(hold.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void NothingHappensAfterCompletionUntilReset()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f));
            hold.Tick(2, 1f);

            Assert.That(hold.Tick(2, 1f), Is.EqualTo(CoopHoldStep.None));
            Assert.That(hold.Tick(0, 1f), Is.EqualTo(CoopHoldStep.None), "leaving after completion is not an interruption");
            Assert.That(hold.IsCompleted, Is.True);

            hold.Reset();

            Assert.That(hold.IsCompleted, Is.False);
            Assert.That(hold.ElapsedSeconds, Is.Zero);
            Assert.That(hold.Tick(2, 0.5f), Is.EqualTo(CoopHoldStep.Started));
        }

        [Test]
        public void SomeoneLeavingStopsAndKeepsTheElapsedByDefault()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f));
            hold.Tick(2, 0.5f);

            Assert.That(hold.Tick(1, 0.25f), Is.EqualTo(CoopHoldStep.Interrupted));
            Assert.That(hold.IsRunning, Is.False);
            Assert.That(hold.ElapsedSeconds, Is.EqualTo(0.5f), "the frame someone left does not count");

            Assert.That(hold.Tick(1, 0.25f), Is.EqualTo(CoopHoldStep.None), "interrupted only once");
            Assert.That(hold.ElapsedSeconds, Is.EqualTo(0.5f));

            Assert.That(hold.Tick(2, 0.25f), Is.EqualTo(CoopHoldStep.Started), "resumes when the count is back");
            Assert.That(hold.ElapsedSeconds, Is.EqualTo(0.75f));
        }

        [Test]
        public void SomeoneLeavingResetsTheElapsedWhenTheSpecSaysSo()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 1f, resetOnInterrupt: true));
            hold.Tick(2, 0.5f);

            Assert.That(hold.Tick(0, 0.25f), Is.EqualTo(CoopHoldStep.Interrupted));
            Assert.That(hold.ElapsedSeconds, Is.Zero);
            Assert.That(hold.Progress, Is.Zero);
        }

        [Test]
        public void ZeroHoldSecondsCompletesAsSoonAsEnoughPeopleHold()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 0f));

            Assert.That(hold.Tick(1, 0f), Is.EqualTo(CoopHoldStep.None));
            Assert.That(hold.Progress, Is.Zero);
            Assert.That(hold.Tick(2, 0f), Is.EqualTo(CoopHoldStep.Completed));
            Assert.That(hold.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void TheLogDescribesTheCountAndTheElapsed()
        {
            var hold = new CoopHold(new CoopHoldSpec(2, 3f));
            hold.Tick(1, 0f);

            Assert.That(CoopHoldLog.Describe(hold), Is.EqualTo("holding=1/2 elapsed=0.00/3.00"));

            hold.Tick(2, 0.5f);
            Assert.That(CoopHoldLog.Describe(hold), Is.EqualTo("holding=2/2 elapsed=0.50/3.00"));
            Assert.Throws<ArgumentNullException>(() => CoopHoldLog.Describe(null));
        }
    }
}
