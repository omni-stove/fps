using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ChargeTriggerTests
    {
        private const float _tolerance = 1e-5f;

        [Test]
        public void ChargesWhileHeldAndFiresOnRelease()
        {
            var trigger = Create(new ChargeSpec(2f, 0.25f, 3f));

            Assert.That(trigger.Press(), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.ChargeFraction, Is.EqualTo(0.25f).Within(_tolerance));
            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.ChargeFraction, Is.EqualTo(0.5f).Within(_tolerance));

            Assert.That(trigger.Release(), Is.EqualTo(1));
            Assert.That(trigger.ChargeFraction, Is.Zero);
        }

        [Test]
        public void TheChargeStopsAtFull()
        {
            var trigger = Create(new ChargeSpec(1f, 0f, 2f));

            trigger.Press();
            trigger.Tick(5f);

            Assert.That(trigger.ChargeFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ATooShortPressDoesNotFire()
        {
            var trigger = Create(new ChargeSpec(1f, 0.5f, 2f));

            trigger.Press();
            trigger.Tick(0.4f);

            Assert.That(trigger.Release(), Is.Zero);
            Assert.That(trigger.Press(), Is.Zero);
            Assert.That(trigger.Release(), Is.Zero, "a tap does not fire");
        }

        [Test]
        public void ATooShortPressFiresWhenTheSpecSaysSo()
        {
            var trigger = Create(new ChargeSpec(1f, 0.5f, 2f, firesBelowMin: true));

            trigger.Press();
            trigger.Tick(0.1f);

            Assert.That(trigger.Release(), Is.EqualTo(1));
        }

        [Test]
        public void ReachingTheMinimumByFramesFires()
        {
            var trigger = Create(new ChargeSpec(1f, 0.3f, 2f));

            trigger.Press();
            for (var i = 0; i < 18; i++)
            {
                trigger.Tick(1f / 60f);
            }

            Assert.That(trigger.Release(), Is.EqualTo(1));
        }

        [Test]
        public void FiresOnceWhenFullAndKeepsTheFullChargeUntilReleased()
        {
            var trigger = Create(new ChargeSpec(1f, 0.2f, 2f, firesWhenFull: true));

            trigger.Press();
            Assert.That(trigger.Tick(0.6f), Is.Zero);
            Assert.That(trigger.Tick(0.6f), Is.EqualTo(1));
            Assert.That(trigger.ChargeFraction, Is.EqualTo(1f));
            Assert.That(trigger.Tick(1f), Is.Zero);
            Assert.That(trigger.Release(), Is.Zero, "the shot was already fired");
            Assert.That(trigger.ChargeFraction, Is.Zero);
        }

        [Test]
        public void CancelDropsTheChargeWithoutFiring()
        {
            var trigger = Create(new ChargeSpec(1f, 0.2f, 2f));

            trigger.Press();
            trigger.Tick(0.8f);
            trigger.Cancel();

            Assert.That(trigger.ChargeFraction, Is.Zero);
            Assert.That(trigger.Release(), Is.Zero, "the cancelled charge is not fired on release");
            Assert.That(trigger.Tick(1f), Is.Zero);
        }

        [Test]
        public void TheRestOfABurstFiresWithTheChargeItWasReleasedWith()
        {
            var spec = new TriggerSpec(FireMode.Semi, 0.5f, new BurstSpec(3, 0.1f), new ChargeSpec(1f, 0.25f, 3f));
            var trigger = TriggerModels.For(spec);

            trigger.Press();
            trigger.Tick(0.6f);
            Assert.That(trigger.Release(), Is.EqualTo(1));

            Assert.That(trigger.Tick(0.1f), Is.EqualTo(1));
            Assert.That(trigger.ChargeFraction, Is.EqualTo(0.6f).Within(_tolerance), "the second round keeps the released charge");
            Assert.That(trigger.Tick(0.1f), Is.EqualTo(1));
            Assert.That(trigger.ChargeFraction, Is.EqualTo(0.6f).Within(_tolerance));
            Assert.That(trigger.Tick(0.1f), Is.Zero);
            Assert.That(trigger.ChargeFraction, Is.Zero, "the gauge empties once the burst is over");
        }

        [Test]
        public void TheInnerTriggerSpacesTheShots()
        {
            var trigger = Create(new ChargeSpec(0.1f, 0f, 2f), fireInterval: 1f);

            trigger.Press();
            trigger.Tick(0.1f);
            Assert.That(trigger.Release(), Is.EqualTo(1));

            trigger.Press();
            trigger.Tick(0.1f);
            Assert.That(trigger.Release(), Is.Zero, "still within the fire interval");

            trigger.Press();
            trigger.Tick(1f);
            Assert.That(trigger.Release(), Is.EqualTo(1));
        }

        [Test]
        public void AnAutoChargeWeaponDoesNotFireWhileCharging()
        {
            var trigger = (ChargeTrigger)TriggerModels.For(new TriggerSpec(FireMode.Auto, 0.1f, charge: new ChargeSpec(1f, 0f, 2f)));

            Assert.That(trigger.Press(), Is.Zero);
            Assert.That(trigger.Tick(0.5f), Is.Zero);
            Assert.That(trigger.Tick(0.5f), Is.Zero);
            Assert.That(trigger.Release(), Is.EqualTo(1));
            Assert.That(trigger.Tick(1f), Is.Zero);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.75f, 0.5f)]
        [TestCase(3f, 1f)]
        public void TheFractionFollowsTheHeldSeconds(double heldSeconds, float expected)
        {
            Assert.That(ChargeTrigger.Fraction(new ChargeSpec(1.5f, 0f, 2f), heldSeconds), Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void TheRequestedChargeIsCappedByTheHeldTime()
        {
            var charge = new ChargeSpec(2f, 0.25f, 3f);

            Assert.That(ChargeTrigger.Reachable(charge, 1f, 1f), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(ChargeTrigger.Reachable(charge, 0.3f, 10f), Is.EqualTo(0.3f).Within(_tolerance));
            Assert.That(ChargeTrigger.Reachable(charge, 5f, 10f), Is.EqualTo(1f));
            Assert.That(ChargeTrigger.Reachable(charge, -1f, 10f), Is.Zero);
            Assert.That(ChargeTrigger.Reachable(charge, float.NaN, 10f), Is.Zero);
        }

        [Test]
        public void FiresOnlyFromTheMinimumUnlessTheSpecFiresBelowIt()
        {
            Assert.That(ChargeTrigger.Fires(new ChargeSpec(1f, 0.4f, 2f), 0.39f), Is.False);
            Assert.That(ChargeTrigger.Fires(new ChargeSpec(1f, 0.4f, 2f), 0.4f), Is.True);
            Assert.That(ChargeTrigger.Fires(new ChargeSpec(1f, 0.4f, 2f, firesBelowMin: true), 0f), Is.True);
        }

        [TestCase(0f, 1.2f)]
        [TestCase(0.05f, 1.2f)]
        [TestCase(0.2f, 1.4f)]
        [TestCase(0.6f, 2.2f)]
        [TestCase(1f, 3f)]
        [TestCase(2f, 3f)]
        public void TheDamageMultiplierGrowsWithTheChargeFromTheMinimum(float fraction, float expected)
        {
            var charge = new ChargeSpec(1f, 0.1f, 3f);

            Assert.That(ChargeScaling.DamageMultiplier(charge, fraction), Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void TheSpeedMultiplierGrowsWithTheCharge()
        {
            var charge = new ChargeSpec(1f, 0f, 1f, fullChargeSpeedMultiplier: 2f);

            Assert.That(ChargeScaling.SpeedMultiplier(charge, 0f), Is.EqualTo(1f));
            Assert.That(ChargeScaling.SpeedMultiplier(charge, 0.5f), Is.EqualTo(1.5f).Within(_tolerance));
            Assert.That(ChargeScaling.SpeedMultiplier(charge, 1f), Is.EqualTo(2f).Within(_tolerance));
        }

        [Test]
        public void AWeaponWithoutChargeIsNotScaled()
        {
            Assert.That(ChargeScaling.DamageMultiplier(null, 1f), Is.EqualTo(1f));
            Assert.That(ChargeScaling.SpeedMultiplier(null, 1f), Is.EqualTo(1f));
        }

        private static ChargeTrigger Create(ChargeSpec charge, float fireInterval = 0f)
        {
            return (ChargeTrigger)TriggerModels.For(new TriggerSpec(FireMode.Semi, fireInterval, charge: charge));
        }
    }
}
