using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MeleeChargeTests
    {
        private static readonly MeleeWeaponSpec _spec = new MeleeWeaponSpec(
            new MeleeSpec(30, 2f, 70f, 0.4f, 0.1f, 0.1f, 0.2f),
            new MeleeSpec(90, 3f, 110f, 0.8f, 0.3f, 0.5f, 1f),
            0.2f,
            0.8f);

        [Test]
        public void NothingIsReleasedWithoutAPress()
        {
            var charge = new MeleeCharge(_spec);

            charge.Advance(1f);

            Assert.That(charge.IsHolding, Is.False);
            Assert.That(charge.Fraction, Is.Zero);
            Assert.That(charge.TryRelease(out var fraction), Is.False);
            Assert.That(fraction, Is.Zero);
        }

        [Test]
        public void AQuickClickReleasesWithoutCharge()
        {
            var charge = new MeleeCharge(_spec);

            charge.Press();
            charge.Advance(0.1f);

            Assert.That(charge.TryRelease(out var fraction), Is.True);
            Assert.That(fraction, Is.Zero);
            Assert.That(charge.IsHolding, Is.False);
        }

        [Test]
        public void HoldingChargesUntilFullAndReleasingStartsOver()
        {
            var charge = new MeleeCharge(_spec);

            charge.Press();
            charge.Advance(0.6f);
            Assert.That(charge.HeldSeconds, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(charge.Fraction, Is.EqualTo(0.5f).Within(1e-5f));

            charge.Advance(2f);
            Assert.That(charge.Fraction, Is.EqualTo(1f));

            Assert.That(charge.TryRelease(out var fraction), Is.True);
            Assert.That(fraction, Is.EqualTo(1f));
            Assert.That(charge.HeldSeconds, Is.Zero);
            Assert.That(charge.TryRelease(out _), Is.False, "a release counts once");
        }

        [Test]
        public void DroppingForgetsTheHold()
        {
            var charge = new MeleeCharge(_spec);

            charge.Press();
            charge.Advance(0.6f);
            charge.Drop();

            Assert.That(charge.IsHolding, Is.False);
            Assert.That(charge.TryRelease(out _), Is.False);
        }

        [Test]
        public void BadInputIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new MeleeCharge(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeCharge(_spec).Advance(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeCharge(_spec).Advance(float.NaN));
        }
    }
}
