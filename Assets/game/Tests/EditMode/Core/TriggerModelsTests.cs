using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TriggerModelsTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private static WeaponSpec Spec(TriggerSpec trigger)
        {
            return new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, trigger);
        }

        [Test]
        public void AutoFiresOnPressAndThenEveryIntervalWhileHeld()
        {
            var trigger = new AutoTrigger(0.125f);

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(0.0625f), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.0625f), Is.EqualTo(1));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(1f), Is.EqualTo(0));
        }

        [Test]
        public void SemiFiresOnlyOnPressOutsideTheInterval()
        {
            var trigger = new SemiTrigger(0.25f);

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(1f), Is.EqualTo(0));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(0.125f), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(0));
        }

        [Test]
        public void TheBaseTriggersKeepTheFireIntervalAndNeverChargeOrBlockAds()
        {
            foreach (var trigger in new ITriggerModel[] { new AutoTrigger(0.1f), new SemiTrigger(0.2f) })
            {
                Assert.That(trigger.BlocksAds, Is.False);
                Assert.That(trigger.ChargeFraction, Is.EqualTo(0f));
            }

            Assert.That(new AutoTrigger(0.1f).MinShotSpacingSeconds, Is.EqualTo(0.1f));
            Assert.That(new SemiTrigger(0.2f).MinShotSpacingSeconds, Is.EqualTo(0.2f));
        }

        [Test]
        public void PicksTheBaseTriggerFromTheFireMode()
        {
            Assert.That(TriggerModels.For(Spec(new TriggerSpec(FireMode.Auto, 0.1f))), Is.TypeOf<AutoTrigger>());
            Assert.That(TriggerModels.For(Spec(new TriggerSpec(FireMode.Semi, 0.1f))), Is.TypeOf<SemiTrigger>());
            Assert.That(TriggerModels.For(Spec(null)), Is.TypeOf<AutoTrigger>());
        }

        [Test]
        public void WrapsTheBaseTriggerWithEachPartFromInsideOut()
        {
            var burst = new BurstSpec(3, 0.05f);
            var spinUp = new SpinUpSpec(0.5f, 2f, 1f);
            var charge = new ChargeSpec(1f, 0.2f, 2f);
            var cycle = new CycleSpec(0.8f, leavesAds: true);

            var model = TriggerModels.For(new TriggerSpec(FireMode.Semi, 0.5f, burst, charge, spinUp, cycle));

            Assert.That(model, Is.TypeOf<CycleTrigger>());
            Assert.That(((CycleTrigger)model).Spec, Is.SameAs(cycle));
            Assert.That(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0.5f, burst)), Is.TypeOf<BurstTrigger>());
            Assert.That(TriggerModels.For(new TriggerSpec(FireMode.Auto, 0.5f, spinUp: spinUp)), Is.TypeOf<SpinUpTrigger>());
            Assert.That(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0.5f, charge: charge)), Is.TypeOf<ChargeTrigger>());
        }

        [Test]
        public void ABeamWeaponCountsItsShotsByTheBeamAndStillWrapsTheParts()
        {
            var beam = new BeamSpec(0.125f);
            var round = new RoundSpec(RoundDelivery.Beam, beam: beam);

            var model = TriggerModels.For(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, new TriggerSpec(FireMode.Auto, 1f), round: round));
            var wrapped = TriggerModels.For(
                new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, new TriggerSpec(FireMode.Auto, 1f, spinUp: new SpinUpSpec(0.5f, 2f, 1f)), round: round));

            Assert.That(model, Is.TypeOf<BeamState>());
            Assert.That(((BeamState)model).Spec, Is.SameAs(beam));
            Assert.That(wrapped, Is.TypeOf<SpinUpTrigger>());
            Assert.That(wrapped.MinShotSpacingSeconds, Is.EqualTo(0f));
            Assert.That(model.Press(), Is.EqualTo(1));
            Assert.That(model.Tick(0.25f), Is.EqualTo(2), "the beam interval, not the fire interval");

            Assert.That(wrapped.Press(), Is.EqualTo(0), "not spun up yet");
            Assert.That(wrapped.Tick(0.5f), Is.EqualTo(1), "the beam starts once spun up");
            Assert.That(wrapped.Tick(0.25f), Is.EqualTo(2), "the spun-up beam keeps the beam interval");
            Assert.That(wrapped.Release(), Is.EqualTo(0));
            Assert.That(wrapped.Tick(0.25f), Is.EqualTo(0));
        }

        [Test]
        public void ASprayWeaponCountsItsShotsByTheSprayTicksWhileHeld()
        {
            var round = new RoundSpec(RoundDelivery.Spray, 8f, spray: new SpraySpec(0.1f, 15f));

            var model = TriggerModels.For(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, new TriggerSpec(FireMode.Auto, 1f), round: round));

            Assert.That(model, Is.TypeOf<BeamState>());
            Assert.That(((BeamState)model).Spec.TickSeconds, Is.EqualTo(0.1f));
            Assert.That(model.Press(), Is.EqualTo(1));
            Assert.That(model.Tick(0.3f), Is.EqualTo(3), "the spray interval, not the fire interval");
            Assert.That(model.Release(), Is.EqualTo(0));
            Assert.That(model.Tick(0.3f), Is.EqualTo(0), "nothing comes out once released");
        }

        [Test]
        public void AWeaponFiresEveryBeamTickEvenWhenTwoLandInOneFrame()
        {
            var weapon = new Weapon(new WeaponSpec(
                _rifleId, null, WeaponCategory.AssaultRifle, round: new RoundSpec(RoundDelivery.Beam, beam: new BeamSpec(0.125f))));
            var full = weapon.AmmoInMagazine;

            Assert.That(weapon.Trigger.Press(), Is.EqualTo(1));
            Assert.That(weapon.TryFire(), Is.True);
            weapon.Tick(0.25f);
            var ticks = weapon.Trigger.Tick(0.25f);
            for (var i = 0; i < ticks; i++)
            {
                Assert.That(weapon.TryFire(), Is.True);
            }

            Assert.That(ticks, Is.EqualTo(2));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(full - 3), "every tick uses a round");
        }

        [Test]
        public void ACycleKeepsTheTriggerFromFiringAndLeavesAdsUntilItEnds()
        {
            var trigger = new CycleTrigger(new CycleSpec(1f, leavesAds: true), new SemiTrigger(0.25f));

            Assert.That(trigger.BlocksAds, Is.False);
            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.IsCycling, Is.True);
            Assert.That(trigger.BlocksAds, Is.True);

            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(0), "the inner interval is over but the bolt is still being worked");
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.BlocksAds, Is.True);

            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.IsCycling, Is.False);
            Assert.That(trigger.BlocksAds, Is.False);
            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.BlocksAds, Is.True);
        }

        [Test]
        public void ACycleThatKeepsAdsStillStopsShots()
        {
            var trigger = new CycleTrigger(new CycleSpec(1f), new SemiTrigger(0.25f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            trigger.Release();
            Assert.That(trigger.IsCycling, Is.True);
            Assert.That(trigger.BlocksAds, Is.False);
            trigger.Tick(0.75f);
            Assert.That(trigger.Press(), Is.EqualTo(0));
        }

        [Test]
        public void AHeldAutoTriggerFiresAgainAsSoonAsTheCycleEnds()
        {
            var trigger = new CycleTrigger(new CycleSpec(0.5f), new AutoTrigger(0.125f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(0.25f), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.125f), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.125f), Is.EqualTo(1), "the shot the inner trigger fires in the tick that ends the cycle is kept");
            Assert.That(trigger.IsCycling, Is.True, "that shot starts the next cycle");
        }

        [Test]
        public void APressDuringTheCycleDoesNotDelayTheFirstPressAfterIt()
        {
            var trigger = new CycleTrigger(new CycleSpec(1.1f, leavesAds: true), new SemiTrigger(0.2f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            trigger.Release();
            Assert.That(trigger.Tick(1f), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(0));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.15f), Is.EqualTo(0));

            Assert.That(trigger.Press(), Is.EqualTo(1), "the press inside the cycle must not start the inner fire interval");
        }

        [Test]
        public void ATriggerPressedDuringTheCycleAndStillHeldFiresWhenItEnds()
        {
            var trigger = new CycleTrigger(new CycleSpec(1f), new SemiTrigger(0.2f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            trigger.Release();
            trigger.Tick(0.5f);
            Assert.That(trigger.Press(), Is.EqualTo(0));

            Assert.That(trigger.Tick(0.5f), Is.EqualTo(1));
            Assert.That(trigger.IsCycling, Is.True);
        }

        [Test]
        public void AHeldAutoTriggerKeepsTheCycleAsItsSpacing()
        {
            var trigger = new CycleTrigger(new CycleSpec(0.5f), new AutoTrigger(0.4f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(0.4f), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.1f), Is.EqualTo(1), "fires at the end of the cycle, not one inner interval after a dropped shot");
            Assert.That(trigger.Tick(0.4f), Is.EqualTo(0));
            Assert.That(trigger.Tick(0.1f), Is.EqualTo(1));
        }

        [Test]
        public void ReleasingDuringTheCycleStopsAHeldAutoTrigger()
        {
            var trigger = new CycleTrigger(new CycleSpec(0.5f), new AutoTrigger(0.1f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            trigger.Tick(0.25f);
            Assert.That(trigger.Release(), Is.EqualTo(0));

            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.IsCycling, Is.False);
        }

        [Test]
        public void CancellingForgetsAPressMadeDuringTheCycleButKeepsTheCycle()
        {
            var trigger = new CycleTrigger(new CycleSpec(1f), new SemiTrigger(0.2f));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            trigger.Release();
            trigger.Tick(0.5f);
            Assert.That(trigger.Press(), Is.EqualTo(0));

            trigger.Cancel();
            Assert.That(trigger.IsCycling, Is.True);
            Assert.That(trigger.Tick(0.5f), Is.EqualTo(0));
            Assert.That(trigger.IsCycling, Is.False);
        }

        [Test]
        public void TheCycleSetsTheShortestSpacingBetweenShots()
        {
            Assert.That(new CycleTrigger(new CycleSpec(1f), new SemiTrigger(0.25f)).MinShotSpacingSeconds, Is.EqualTo(1f));
            Assert.That(new CycleTrigger(new CycleSpec(0.25f), new SemiTrigger(1f)).MinShotSpacingSeconds, Is.EqualTo(1f));

            var weapon = new Weapon(Spec(new TriggerSpec(FireMode.Semi, 0.25f, cycle: new CycleSpec(1f, leavesAds: true))));
            Assert.That(weapon.TryFire(), Is.True);
            weapon.Tick(0.5f);
            Assert.That(weapon.TryFire(), Is.False);
            weapon.Tick(0.5f);
            Assert.That(weapon.TryFire(), Is.True);

            var budget = new ShotBudget(TriggerModels.For(new TriggerSpec(FireMode.Semi, 0.25f, cycle: new CycleSpec(1f))), burst: 1);
            Assert.That(budget.TryConsume(0d), Is.True);
            Assert.That(budget.TryConsume(0.5d), Is.False);
            Assert.That(budget.TryConsume(1d), Is.True);
        }

        [Test]
        public void TheWeaponSpacesItsShotsByTheTrigger()
        {
            var weapon = new Weapon(Spec(new TriggerSpec(FireMode.Auto, 0.25f)));

            Assert.That(weapon.Trigger.MinShotSpacingSeconds, Is.EqualTo(0.25f));
            Assert.That(weapon.TryFire(), Is.True);
            weapon.Tick(0.125f);
            Assert.That(weapon.TryFire(), Is.False);
            weapon.Tick(0.125f);
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void TheShotBudgetRecoversAtTheTriggersSpacing()
        {
            var budget = new ShotBudget(TriggerModels.For(new TriggerSpec(FireMode.Auto, 0.5f)), burst: 1);

            Assert.That(budget.MinShotSpacingSeconds, Is.EqualTo(0.5f));
            Assert.That(budget.TryConsume(0d), Is.True);
            Assert.That(budget.TryConsume(0.25d), Is.False);
            Assert.That(budget.TryConsume(0.5d), Is.True);
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => TriggerModels.For((WeaponSpec)null));
            Assert.Throws<ArgumentNullException>(() => TriggerModels.For((TriggerSpec)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => TriggerModels.For((FireMode)99, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoTrigger(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SemiTrigger(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoTrigger(0.1f).Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => new BurstTrigger(null, new SemiTrigger(0.1f)));
            Assert.Throws<ArgumentNullException>(() => new CycleTrigger(new CycleSpec(0.8f), null));
            Assert.Throws<ArgumentNullException>(() => new CycleTrigger(null, new SemiTrigger(0.1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CycleTrigger(new CycleSpec(0.8f), new SemiTrigger(0.1f)).Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => new ShotBudget((ITriggerModel)null));
        }
    }
}
