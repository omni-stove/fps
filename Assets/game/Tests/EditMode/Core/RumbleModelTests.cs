using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RumbleModelTests
    {
        [Test]
        public void OverlappingPulsesTakeTheStrongerValueForEachMotor()
        {
            var model = new RumbleModel();

            model.Add(new RumblePulse(0.2f, 0.8f, 0.1f));
            model.Add(new RumblePulse(0.6f, 0.3f, 0.5f));

            Assert.That(model.ActiveCount, Is.EqualTo(2));
            Assert.That(model.Current.Low, Is.EqualTo(0.6f), "not 0.8: overlapping pulses are not added up");
            Assert.That(model.Current.High, Is.EqualTo(0.8f));
        }

        [Test]
        public void APulseEndsAfterItsSecondsAndTheLongerOneStays()
        {
            var model = new RumbleModel();
            model.Add(new RumblePulse(0.2f, 0.8f, 0.1f));
            model.Add(new RumblePulse(0.6f, 0.3f, 0.5f));

            var stoppedAfterShort = model.Tick(0.1f);

            Assert.That(stoppedAfterShort, Is.False, "the longer pulse still runs");
            Assert.That(model.ActiveCount, Is.EqualTo(1));
            Assert.That(model.Current, Is.EqualTo(new RumbleMotors(0.6f, 0.3f)));

            var stoppedAfterLong = model.Tick(0.5f);

            Assert.That(stoppedAfterLong, Is.True, "the last pulse ran out");
            Assert.That(model.ActiveCount, Is.Zero);
            Assert.That(model.Current.IsZero, Is.True);
            Assert.That(model.Tick(1f), Is.False, "nothing was running");
        }

        [Test]
        public void AShorterPulseStillRunsBeforeItsSecondsAreUsed()
        {
            var model = new RumbleModel();
            model.Add(new RumblePulse(0.5f, 0.5f, 0.3f));

            model.Tick(0.2f);

            Assert.That(model.Current, Is.EqualTo(new RumbleMotors(0.5f, 0.5f)));
        }

        [Test]
        public void ASilentPulseIsNotKept()
        {
            var model = new RumbleModel();

            Assert.That(model.Add(RumblePulse.None), Is.False);
            Assert.That(model.Add(new RumblePulse(0f, 0f, 1f)), Is.False);
            Assert.That(model.Add(new RumblePulse(1f, 1f, 0f)), Is.False);
            Assert.That(model.ActiveCount, Is.Zero);
        }

        [Test]
        public void ResetDropsEveryPulse()
        {
            var model = new RumbleModel();
            model.Add(new RumblePulse(1f, 1f, 1f));

            model.Reset();

            Assert.That(model.ActiveCount, Is.Zero);
            Assert.That(model.Current, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void ExplosionFadesWithDistanceUntilTheRadius()
        {
            var pulse = new RumblePulse(1f, 0.6f, 0.45f);

            var center = RumbleModel.ForExplosion(pulse, 0f, 25f);
            var half = RumbleModel.ForExplosion(pulse, 12.5f, 25f);
            var edge = RumbleModel.ForExplosion(pulse, 25f, 25f);
            var far = RumbleModel.ForExplosion(pulse, 100f, 25f);

            Assert.That(center, Is.EqualTo(pulse));
            Assert.That(half.Low, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(half.High, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(half.Seconds, Is.EqualTo(0.45f), "distance does not shorten the pulse");
            Assert.That(edge.IsSilent, Is.True);
            Assert.That(far.IsSilent, Is.True);
        }

        [Test]
        public void ExplosionRejectsInvalidValues()
        {
            var pulse = new RumblePulse(1f, 1f, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.ForExplosion(pulse, -1f, 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.ForExplosion(pulse, float.NaN, 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.ForExplosion(pulse, float.PositiveInfinity, 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.ForExplosion(pulse, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.ForExplosion(pulse, 1f, float.PositiveInfinity));
        }

        [Test]
        public void TheSettingsStrengthScalesBothMotors()
        {
            var raw = new RumbleMotors(0.8f, 0.4f);

            var full = RumbleModel.Apply(raw, true, 1f);
            var half = RumbleModel.Apply(raw, true, 0.5f);

            Assert.That(full, Is.EqualTo(raw));
            Assert.That(half.Low, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(half.High, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void TurnedOffOrZeroStrengthDoesNotRumble()
        {
            var raw = new RumbleMotors(0.8f, 0.4f);

            Assert.That(RumbleModel.Apply(raw, false, 1f), Is.EqualTo(RumbleMotors.Zero));
            Assert.That(RumbleModel.Apply(raw, true, 0f), Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void SampleAppliesTheSettingsToTheStrongestPulse()
        {
            var model = new RumbleModel();
            model.Add(new RumblePulse(0.2f, 0.8f, 0.1f));
            model.Add(new RumblePulse(0.6f, 0.3f, 0.5f));

            var sample = model.Sample(true, 0.5f);

            Assert.That(sample.Low, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(sample.High, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(model.Sample(false, 0.5f), Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void InvalidStrengthOrTimeIsRejected()
        {
            var model = new RumbleModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.Apply(RumbleMotors.Zero, true, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.Apply(RumbleMotors.Zero, true, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RumbleModel.Apply(RumbleMotors.Zero, true, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(float.NaN));
        }

        [Test]
        public void APulseRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RumblePulse(1.1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RumblePulse(0f, -0.1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RumblePulse(0f, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RumblePulse(0f, 0f, float.PositiveInfinity));
        }

        [Test]
        public void TheLogLinesNameTheSourceAndValues()
        {
            Assert.That(
                RumbleLog.Pulse("explosion", new RumblePulse(0.5f, 0.3f, 0.45f), "weapon=frag distance=12.5"),
                Is.EqualTo("[Rumble] pulse source=explosion low=0.500 high=0.300 seconds=0.45 weapon=frag distance=12.5"));
            Assert.That(
                RumbleLog.Pulse(null, new RumblePulse(0.15f, 0.35f, 0.08f), null),
                Is.EqualTo("[Rumble] pulse source=unknown low=0.150 high=0.350 seconds=0.08"));
            Assert.That(RumbleLog.Stopped("paused"), Is.EqualTo("[Rumble] stopped reason=paused"));
            Assert.That(RumbleLog.DeviceChanged("Pad A", null), Is.EqualTo("[Rumble] device changed from=Pad_A to=none"));
            Assert.That(RumbleLog.Distance(12.54f), Is.EqualTo("distance=12.5"));
            Assert.That(RumbleLog.Amount(25), Is.EqualTo("amount=25"));
            Assert.That(RumbleLog.Weapon(string.Empty), Is.EqualTo("weapon=none"));
        }
    }
}
