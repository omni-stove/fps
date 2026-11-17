using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BeamStateTests
    {
        private static int Hold(BeamState beam, float seconds, int frames)
        {
            var ticks = beam.Start();
            for (var i = 0; i < frames; i++)
            {
                ticks += beam.Tick(seconds / frames);
            }

            return ticks;
        }

        [TestCase(0.125f, 1f, 64, 9)]
        [TestCase(0.1f, 3f, 180, 31)]
        [TestCase(0.05f, 2f, 144, 41)]
        [TestCase(0.1f, 0.05f, 3, 1)]
        public void HoldingTheBeamTicksOnPressAndThenEveryInterval(float tickSeconds, float seconds, int frames, int expected)
        {
            var beam = new BeamState(new BeamSpec(tickSeconds));

            Assert.That(Hold(beam, seconds, frames), Is.EqualTo(expected));
            Assert.That(beam.IsFiring, Is.True);
            Assert.That(beam.FiringSeconds, Is.EqualTo(seconds).Within(1e-4f));
        }

        [Test]
        public void OneLongFrameReturnsEveryTickItCrossed()
        {
            var beam = new BeamState(new BeamSpec(0.1f));

            Assert.That(beam.Start(), Is.EqualTo(1));
            Assert.That(beam.Tick(0.05f), Is.EqualTo(0));
            Assert.That(beam.Tick(1f), Is.EqualTo(10));
            Assert.That(beam.Tick(0.05f), Is.EqualTo(1));
        }

        [Test]
        public void ReleasingStopsTheTicksAndTheFiringTime()
        {
            var beam = new BeamState(new BeamSpec(0.1f));
            beam.Start();
            beam.Tick(0.25f);

            beam.Stop();

            Assert.That(beam.IsFiring, Is.False);
            Assert.That(beam.Tick(5f), Is.EqualTo(0));
            Assert.That(beam.FiringSeconds, Is.EqualTo(0.25f));
        }

        [Test]
        public void PressingAgainCannotTickFasterThanTheInterval()
        {
            var beam = new BeamState(new BeamSpec(0.125f));
            Assert.That(beam.Start(), Is.EqualTo(1));
            beam.Stop();

            Assert.That(beam.Tick(0.0625f), Is.EqualTo(0));
            Assert.That(beam.Start(), Is.EqualTo(0), "within the interval of the last tick");
            Assert.That(beam.FiringSeconds, Is.EqualTo(0f));
            Assert.That(beam.Tick(0.0625f), Is.EqualTo(1), "the next tick comes on time");
            beam.Stop();

            beam.Tick(0.125f);
            Assert.That(beam.Start(), Is.EqualTo(1), "the interval has passed");
        }

        [Test]
        public void StartingWhileFiringChangesNothing()
        {
            var beam = new BeamState(new BeamSpec(0.1f));
            beam.Start();
            beam.Tick(0.05f);

            Assert.That(beam.Start(), Is.EqualTo(0));
            Assert.That(beam.FiringSeconds, Is.EqualTo(0.05f));
        }

        [Test]
        public void AsATriggerItHoldsWhilePressedAndNeverSpacesOrChargesOrBlocksAds()
        {
            ITriggerModel trigger = new BeamState(new BeamSpec(0.125f));

            Assert.That(trigger.MinShotSpacingSeconds, Is.EqualTo(0f));
            Assert.That(trigger.BlocksAds, Is.False);
            Assert.That(trigger.ChargeFraction, Is.EqualTo(0f));
            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(0.25f), Is.EqualTo(2));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(((BeamState)trigger).IsFiring, Is.False);
            Assert.That(trigger.Tick(1f), Is.EqualTo(0));

            trigger.Press();
            trigger.Cancel();
            Assert.That(((BeamState)trigger).IsFiring, Is.False, "cancel stops the beam");
            Assert.That(trigger.Tick(1f), Is.EqualTo(0));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => new BeamState(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeamState(new BeamSpec(0.1f)).Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeamState(new BeamSpec(0.1f)).Tick(float.NaN));
        }
    }
}
