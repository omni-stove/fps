using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class TriggerDriverTests
    {
        private const float Interval = 0.1f;

        [Test]
        public void AutoFiresWhileHeldAndStopsWhenReleased()
        {
            var driver = new TriggerDriver();
            driver.Bind(new AutoTrigger(Interval));

            Assert.That(driver.Pull(FireMode.Auto, true, true), Is.EqualTo(1));
            Assert.That(driver.Pull(FireMode.Auto, true, false), Is.Zero, "within the interval");

            driver.Advance(Interval);
            Assert.That(driver.Pull(FireMode.Auto, true, false), Is.EqualTo(1));

            driver.Advance(Interval);
            Assert.That(driver.Pull(FireMode.Auto, false, false), Is.Zero, "released before the time was spent");
            Assert.That(driver.IsHeld, Is.False);
        }

        [Test]
        public void SemiFiresOncePerPress()
        {
            var driver = new TriggerDriver();
            driver.Bind(new SemiTrigger(Interval));

            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.EqualTo(1));
            driver.Advance(Interval);
            Assert.That(driver.Pull(FireMode.Semi, true, false), Is.Zero, "holding does not fire again");
            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.EqualTo(1), "pressed again");
        }

        [Test]
        public void DropReleasesSoTheNextPressStartsAfresh()
        {
            var driver = new TriggerDriver();
            driver.Bind(new AutoTrigger(Interval));
            Assert.That(driver.Pull(FireMode.Auto, true, true), Is.EqualTo(1));

            driver.Advance(Interval);
            driver.Drop();
            Assert.That(driver.IsHeld, Is.False);

            Assert.That(driver.Pull(FireMode.Auto, true, false), Is.EqualTo(1), "the time passed while dropped still counts");
        }

        [Test]
        public void DropDropsTheRestOfABurst()
        {
            var driver = new TriggerDriver();
            var burst = new BurstTrigger(new BurstSpec(3, Interval), new SemiTrigger(1f));
            driver.Bind(burst);
            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.EqualTo(1));

            driver.Drop();
            driver.Advance(Interval * 4f);
            Assert.That(driver.Pull(FireMode.Semi, false, false), Is.Zero, "the dropped burst does not fire on its own");
        }

        [Test]
        public void SwitchingAwayStopsTheSpinSoSwitchingBackSpinsUpAgain()
        {
            var driver = new TriggerDriver();
            var minigun = TriggerModels.For(new TriggerSpec(FireMode.Auto, Interval, spinUp: new SpinUpSpec(0.5f, 2f, 1f)));
            var spin = (SpinUpTrigger)minigun;
            driver.Bind(minigun);
            driver.Pull(FireMode.Auto, true, true);
            driver.Advance(2f);
            driver.Pull(FireMode.Auto, true, false);
            Assert.That(spin.SpinFraction, Is.EqualTo(1f));

            driver.Bind(new SemiTrigger(Interval));
            driver.Bind(minigun);

            Assert.That(spin.SpinFraction, Is.Zero);
            Assert.That(driver.Pull(FireMode.Auto, true, true), Is.Zero, "spins up again before firing");
        }

        [Test]
        public void SwitchingAwayKeepsTheBoltCycleRunningLeft()
        {
            var driver = new TriggerDriver();
            var bolt = new CycleTrigger(new CycleSpec(1f, false), new SemiTrigger(Interval));
            driver.Bind(bolt);
            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.EqualTo(1));

            driver.Bind(new SemiTrigger(Interval));
            driver.Bind(bolt);

            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.Zero, "the bolt still has to be worked");
        }

        [Test]
        public void BindingAnotherModelDoesNotCarryTheHeldTriggerOrTheTime()
        {
            var driver = new TriggerDriver();
            var first = new AutoTrigger(Interval);
            driver.Bind(first);
            Assert.That(driver.Pull(FireMode.Auto, true, true), Is.EqualTo(1));
            driver.Advance(1f);

            var second = new SemiTrigger(Interval);
            driver.Bind(second);
            Assert.That(driver.Model, Is.SameAs(second));
            Assert.That(driver.IsHeld, Is.False);
            Assert.That(driver.Pull(FireMode.Semi, true, false), Is.Zero, "held from before the switch is not a press");
        }

        [Test]
        public void AChargedShotKeepsTheChargeItWasReleasedWith()
        {
            var driver = new TriggerDriver();
            driver.Bind(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0f, charge: new ChargeSpec(1f, 0.25f, 3f))));

            Assert.That(driver.Pull(FireMode.Semi, true, true), Is.Zero, "pressing only starts the charge");
            driver.Advance(0.5f);
            Assert.That(driver.Pull(FireMode.Semi, true, false), Is.Zero);
            Assert.That(driver.ChargeFraction, Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(driver.Pull(FireMode.Semi, false, false), Is.EqualTo(1));
            Assert.That(driver.ShotChargeFraction, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(driver.ChargeFraction, Is.Zero);
        }

        [Test]
        public void AShotFiredWhenFullCarriesTheFullCharge()
        {
            var driver = new TriggerDriver();
            driver.Bind(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0f, charge: new ChargeSpec(1f, 0.25f, 3f, firesWhenFull: true))));

            driver.Pull(FireMode.Semi, true, true);
            driver.Advance(0.9f);
            Assert.That(driver.Pull(FireMode.Semi, true, false), Is.Zero);
            driver.Advance(0.2f);

            Assert.That(driver.Pull(FireMode.Semi, true, false), Is.EqualTo(1));
            Assert.That(driver.ShotChargeFraction, Is.EqualTo(1f));
            Assert.That(driver.Pull(FireMode.Semi, false, false), Is.Zero, "releasing after the full shot fires nothing");
        }

        [Test]
        public void ATooShortChargeFiresNothing()
        {
            var driver = new TriggerDriver();
            driver.Bind(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0f, charge: new ChargeSpec(1f, 0.5f, 3f))));

            driver.Pull(FireMode.Semi, true, true);
            driver.Advance(0.2f);

            Assert.That(driver.Pull(FireMode.Semi, false, false), Is.Zero);
        }

        [Test]
        public void WithoutAModelNothingFires()
        {
            var driver = new TriggerDriver();
            Assert.That(driver.Pull(FireMode.Auto, true, true), Is.Zero);
            driver.Drop();
            Assert.That(driver.ChargeFraction, Is.Zero);
        }

        [Test]
        public void BotsHoldAutoWeaponsAndTapSemiWeaponsEveryInterval()
        {
            var auto = TestWeapons.RifleSpec();
            var autoDriver = new TriggerDriver();
            autoDriver.Bind(TriggerModels.For(auto));
            Assert.That(autoDriver.PullAsBot(auto), Is.EqualTo(1));
            Assert.That(autoDriver.IsHeld, Is.True);
            autoDriver.Advance(auto.FireInterval);
            Assert.That(autoDriver.PullAsBot(auto), Is.EqualTo(1));
            Assert.That(autoDriver.IsHeld, Is.True, "an auto weapon stays pressed while firing");

            var semi = TestWeapons.HandgunSpec();
            var semiDriver = new TriggerDriver();
            semiDriver.Bind(TriggerModels.For(semi));
            Assert.That(semiDriver.PullAsBot(semi), Is.EqualTo(1));
            Assert.That(semiDriver.PullAsBot(semi), Is.Zero, "within the interval");
            semiDriver.Advance(semi.FireInterval);
            Assert.That(semiDriver.PullAsBot(semi), Is.EqualTo(1), "a semi weapon is pressed again each shot");
        }
    }
}
