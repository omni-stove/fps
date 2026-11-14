using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExplosiveChargeTests
    {
        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(maxHealth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(maxDamage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(damageRadius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(damageRadius: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(damageRadius: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(damageRadius: 2f, fullDamageRadius: 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(fullDamageRadius: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(structureDamageAtCenter: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(chainDelaySeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(chainDelaySeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(burnSeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosivePropSpec(burnSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new ExplosiveCharge(null));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaultValues()
        {
            var spec = ExplosivePropSpec.Default;

            Assert.That(spec.MaxHealth, Is.EqualTo(ExplosivePropSpec.DefaultMaxHealth));
            Assert.That(spec.MaxDamage, Is.EqualTo(ExplosivePropSpec.DefaultMaxDamage));
            Assert.That(spec.DamageRadius, Is.EqualTo(ExplosivePropSpec.DefaultDamageRadius));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(ExplosivePropSpec.DefaultFullDamageRadius));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(ExplosivePropSpec.DefaultStructureDamageAtCenter));
            Assert.That(spec.ChainDelaySeconds, Is.EqualTo(ExplosivePropSpec.DefaultChainDelaySeconds));
            Assert.That(spec.BurnSeconds, Is.EqualTo(ExplosivePropSpec.DefaultBurnSeconds));
            Assert.That(spec.Burns, Is.False);
            Assert.That(spec.Explosion.Radius, Is.EqualTo(spec.DamageRadius));
        }

        [Test]
        public void TheDelayIsTheBurnTimeOrAtLeastTheChainDelayWhenChained()
        {
            var barrel = new ExplosivePropSpec(chainDelaySeconds: 0.3f, burnSeconds: 0f);
            var gasCan = new ExplosivePropSpec(chainDelaySeconds: 0.3f, burnSeconds: 2f);

            Assert.That(barrel.DetonationDelay(chained: false), Is.Zero);
            Assert.That(barrel.DetonationDelay(chained: true), Is.EqualTo(0.3f));
            Assert.That(gasCan.DetonationDelay(chained: false), Is.EqualTo(2f));
            Assert.That(gasCan.DetonationDelay(chained: true), Is.EqualTo(2f));
        }

        [Test]
        public void DamageBelowTheHealthOnlyWearsItDown()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 20));

            Assert.That(charge.ApplyDamage(0, chained: false), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(charge.ApplyDamage(15, chained: false), Is.EqualTo(ExplosiveChargeStep.Damaged));
            Assert.That(charge.Health, Is.EqualTo(5));
            Assert.That(charge.IsTriggered, Is.False);
            Assert.That(charge.Tick(10f), Is.False, "nothing goes off before the health runs out");
            Assert.Throws<ArgumentOutOfRangeException>(() => charge.ApplyDamage(-1, chained: false));
        }

        [Test]
        public void AShotThatEmptiesTheHealthGoesOffOnTheNextTick()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 20));

            Assert.That(charge.ApplyDamage(25, chained: false), Is.EqualTo(ExplosiveChargeStep.Triggered));
            Assert.That(charge.Health, Is.Zero);
            Assert.That(charge.IsTriggered, Is.True);
            Assert.That(charge.IsChained, Is.False);
            Assert.That(charge.HasDetonated, Is.False, "it does not go off inside the hit");

            Assert.That(charge.Tick(0f), Is.True);
            Assert.That(charge.HasDetonated, Is.True);
            Assert.That(charge.Tick(1f), Is.False, "it goes off only once");
        }

        [Test]
        public void AChainedHitWaitsForTheChainDelay()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 20, chainDelaySeconds: 0.5f));

            Assert.That(charge.ApplyDamage(100, chained: true), Is.EqualTo(ExplosiveChargeStep.Triggered));
            Assert.That(charge.IsChained, Is.True);
            Assert.That(charge.RemainingSeconds, Is.EqualTo(0.5f));

            Assert.That(charge.Tick(0.3f), Is.False);
            Assert.That(charge.RemainingSeconds, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(charge.Tick(0.3f), Is.True);
            Assert.That(charge.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void ABurningPropGoesOffAfterTheBurnSeconds()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 5, burnSeconds: 2f));

            Assert.That(charge.ApplyDamage(5, chained: false), Is.EqualTo(ExplosiveChargeStep.Ignited));
            Assert.That(charge.IsBurning, Is.True);
            Assert.That(charge.Tick(1.9f), Is.False);
            Assert.That(charge.IsBurning, Is.True);
            Assert.That(charge.Tick(0.2f), Is.True);
            Assert.That(charge.IsBurning, Is.False, "it stops burning once it goes off");
        }

        [Test]
        public void HitsAfterItWasSetOffChangeNothing()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 5, chainDelaySeconds: 0.3f, burnSeconds: 2f));
            charge.ApplyDamage(5, chained: false);

            Assert.That(charge.ApplyDamage(50, chained: true), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(charge.IsChained, Is.False);
            Assert.That(charge.RemainingSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void TickRejectsInvalidSeconds()
        {
            var charge = new ExplosiveCharge(ExplosivePropSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => charge.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => charge.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => charge.Tick(float.PositiveInfinity));
        }

        [Test]
        public void LogsCarryTheTagAndTheState()
        {
            var charge = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 20, chainDelaySeconds: 0.3f, burnSeconds: 2f));
            charge.ApplyDamage(5, chained: false);

            Assert.That(ExplosivePropLog.Damaged("Can", 5, charge), Is.EqualTo("[Explosive] damaged id=Can dmg=5 hp=15/20"));

            var step = charge.ApplyDamage(20, chained: true);
            Assert.That(ExplosivePropLog.Triggered("Can", step, charge), Is.EqualTo("[Explosive] ignited id=Can chained=true delay=2.00"));

            var barrel = new ExplosiveCharge(new ExplosivePropSpec(maxHealth: 1));
            var barrelStep = barrel.ApplyDamage(1, chained: false);
            Assert.That(
                ExplosivePropLog.Triggered("Barrel", barrelStep, barrel), Is.EqualTo("[Explosive] triggered id=Barrel chained=false delay=0.00"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExplosivePropLog.Triggered("Barrel", ExplosiveChargeStep.Damaged, barrel));

            Assert.That(
                ExplosivePropLog.Exploded("Barrel", true, -12.5f, 0.5f, 14f, 1, 1, 2),
                Is.EqualTo("[Explosive] exploded id=Barrel chained=true at=(-12.5, 0.5, 14.0) damaged=1 killed=1 structures=2"));
            Assert.That(
                ExplosivePropLog.Exploded("Barrel", false, 0f, 0f, 0f, 0, 0, 0), Does.StartWith(ExplosivePropLog.ExplodedPrefix));
            Assert.That(ExplosivePropLog.Hit("Barrel", "Dummy", 150, 1.04f), Is.EqualTo("[Explosive] hit id=Barrel target=Dummy dmg=150 distance=1.0"));
        }
    }
}
