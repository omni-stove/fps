using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
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
