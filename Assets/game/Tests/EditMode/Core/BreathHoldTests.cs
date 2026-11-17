using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BreathHoldTests
    {
        private static BreathHold Breath(float maxHold = 4f, float recover = 2f, float multiplier = 0.25f, float exhausted = 2f)
        {
            var breath = new BreathHold(new BreathHoldSpec(maxHold, recover, multiplier), exhausted, 0f);
            breath.Tick(false, 0f);
            return breath;
        }

        [Test]
        public void StartsFullAndNotHolding()
        {
            var breath = Breath();

            Assert.That(breath.RemainingSeconds, Is.EqualTo(4f));
            Assert.That(breath.RemainingFraction, Is.EqualTo(1f));
            Assert.That(breath.IsHolding, Is.False);
            Assert.That(breath.IsExhausted, Is.False);
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void HoldingSpendsOneSecondPerSecondAndShrinksTheSway()
        {
            var breath = Breath();

            breath.Tick(true, 1.5f);

            Assert.That(breath.IsHolding, Is.True);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(breath.SwayMultiplier, Is.EqualTo(0.25f));
        }

        [Test]
        public void ReleasingEarlyRecoversAtTheRateThatFillsAnEmptyBreathInTheRecoverSeconds()
        {
            var breath = Breath();
            breath.Tick(true, 2f);

            breath.Tick(false, 0.5f);

            Assert.That(breath.IsHolding, Is.False);
            Assert.That(breath.IsExhausted, Is.False);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(3f).Within(1e-5f), "4 s of breath over 2 s is 2 s back per second");
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1f));

            breath.Tick(true, 1f);
            Assert.That(breath.IsHolding, Is.True, "a breath that is not used up can be held again at once");
            Assert.That(breath.RemainingSeconds, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void UsingItUpLocksHoldingForTheRecoverSecondsAndWidensTheSway()
        {
            var breath = Breath();
            breath.Tick(true, 4f);

            Assert.That(breath.IsHolding, Is.False);
            Assert.That(breath.IsExhausted, Is.True);
            Assert.That(breath.RemainingSeconds, Is.Zero);
            Assert.That(breath.SwayMultiplier, Is.EqualTo(2f));

            breath.Tick(false, 1f);
            breath.Tick(true, 0.5f);
            Assert.That(breath.IsHolding, Is.False, "cannot hold while recovering");
            Assert.That(breath.IsExhausted, Is.True);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1.25f).Within(1e-5f), "the sway settles back as the breath returns");

            breath.Tick(false, 0.5f);
            Assert.That(breath.IsExhausted, Is.False);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(4f));
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void TimeLeftAfterRunningOutInOneTickGoesToRecovery()
        {
            var breath = Breath();

            breath.Tick(true, 5f);

            Assert.That(breath.IsExhausted, Is.True);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void KeepingTheButtonDownAfterRecoveringNeedsAFreshPress()
        {
            var breath = Breath();
            breath.Tick(true, 4f);
            breath.Tick(true, 2f);

            Assert.That(breath.IsExhausted, Is.False);
            breath.Tick(true, 1f);
            Assert.That(breath.IsHolding, Is.False);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(4f));

            breath.Tick(false, 0f);
            breath.Tick(true, 1f);
            Assert.That(breath.IsHolding, Is.True);
        }

        [Test]
        public void ResetRefillsAndNeedsAFreshPress()
        {
            var breath = Breath();
            breath.Tick(true, 4f);

            breath.Reset();

            Assert.That(breath.IsExhausted, Is.False);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(4f));
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1f));
            breath.Tick(true, 1f);
            Assert.That(breath.IsHolding, Is.False);
        }

        [Test]
        public void ZeroRecoverSecondsRefillsAtOnce()
        {
            var breath = Breath(recover: 0f);
            breath.Tick(true, 4f);

            Assert.That(breath.IsExhausted, Is.False);
            Assert.That(breath.RemainingSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void AWeaponWithoutBreathHoldNeverHolds()
        {
            var breath = new BreathHold(null);
            breath.Tick(false, 0f);
            breath.Tick(true, 1f);

            Assert.That(breath.IsHolding, Is.False);
            Assert.That(breath.RemainingSeconds, Is.Zero);
            Assert.That(breath.SwayMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void TheSwayMultiplierEasesTowardItsTarget()
        {
            var breath = new BreathHold(new BreathHoldSpec(4f, 2f, 0.2f), 2f, 0.3f);
            breath.Tick(false, 0f);

            breath.Tick(true, 0.1f);
            var first = breath.SwayMultiplier;
            Assert.That(first, Is.LessThan(1f).And.GreaterThan(0.2f));

            breath.Tick(true, 2f);
            Assert.That(breath.SwayMultiplier, Is.LessThan(first).And.EqualTo(0.2f).Within(0.01f));
        }

        [Test]
        public void RejectsUnusableValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BreathHold(null, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BreathHold(null, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BreathHold(null, 2f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Breath().Tick(true, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Breath().Tick(true, float.NaN));
        }

        [Test]
        public void SwayTracesAFigureEightWithinTheAmplitude()
        {
            BreathHold.SwayOffset(0f, 1f, 4f, out var pitch, out var yaw);
            Assert.That(pitch, Is.Zero.Within(1e-6f));
            Assert.That(yaw, Is.Zero.Within(1e-6f));

            BreathHold.SwayOffset(1f, 1f, 4f, out pitch, out yaw);
            Assert.That(yaw, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(pitch, Is.Zero.Within(1e-5f));

            BreathHold.SwayOffset(0.5f, 1f, 4f, out pitch, out _);
            Assert.That(pitch, Is.EqualTo(0.5f).Within(1e-5f));

            BreathHold.SwayOffset(4.7f, 1f, 4f, out var laterPitch, out var laterYaw);
            BreathHold.SwayOffset(0.7f, 1f, 4f, out pitch, out yaw);
            Assert.That(laterPitch, Is.EqualTo(pitch).Within(1e-5f), "repeats every cycle");
            Assert.That(laterYaw, Is.EqualTo(yaw).Within(1e-5f));

            BreathHold.SwayOffset(1.3f, 0f, 4f, out pitch, out yaw);
            Assert.That(pitch, Is.Zero);
            Assert.That(yaw, Is.Zero);
        }

        [Test]
        public void SwayRejectsUnusableValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BreathHold.SwayOffset(0f, -1f, 4f, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => BreathHold.SwayOffset(0f, 1f, 0f, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => BreathHold.SwayOffset(float.NaN, 1f, 4f, out _, out _));
        }
    }
}
