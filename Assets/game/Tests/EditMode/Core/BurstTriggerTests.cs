using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 間隔を 2 のべき乗の分数にしているのは、足し引きで誤差が出ず、ちょうどの時刻で撃つかを確かめられるようにするため。
    public sealed class BurstTriggerTests
    {
        private const string _burstRifleId = "00000000-0000-4000-8000-000000000002";
        private const float RoundInterval = 0.0625f;
        private const float FireInterval = 0.5f;

        private static TriggerSpec Trigger(FireMode mode, int rounds = 3)
        {
            return new TriggerSpec(mode, FireInterval, new BurstSpec(rounds, RoundInterval));
        }

        private static WeaponSpec Spec(FireMode mode, int magazineSize = 30)
        {
            return new WeaponSpec(_burstRifleId, null, WeaponCategory.AssaultRifle, Trigger(mode), new AmmoSpec(magazineSize, 0));
        }

        [Test]
        public void OnePressFiresTheBurstAtTheRoundIntervalAndTheNextAfterTheFireInterval()
        {
            var trigger = TriggerModels.For(Trigger(FireMode.Semi));

            Assert.That(trigger.Press(), Is.EqualTo(1), "the first round leaves on the press");
            Assert.That(trigger.Tick(RoundInterval / 2f), Is.EqualTo(0));
            Assert.That(trigger.Tick(RoundInterval / 2f), Is.EqualTo(1));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(RoundInterval), Is.EqualTo(1), "releasing does not cut the burst short");
            Assert.That(trigger.Tick(1f / 128f), Is.EqualTo(0), "three rounds only");

            Assert.That(trigger.Tick(FireInterval - (RoundInterval * 2f) - (1f / 128f)), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(0), "the fire interval counts from the last round");
            Assert.That(trigger.Tick(RoundInterval * 2f), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(1));
        }

        [Test]
        public void HoldingAnAutoBurstFiresTheNextBurstAfterTheFireInterval()
        {
            var trigger = TriggerModels.For(Trigger(FireMode.Auto));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(RoundInterval), Is.EqualTo(1));
            Assert.That(trigger.Tick(RoundInterval), Is.EqualTo(1));
            Assert.That(trigger.Tick(FireInterval - RoundInterval), Is.EqualTo(0));
            Assert.That(trigger.Tick(RoundInterval), Is.EqualTo(1), "the next burst starts while held");
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(RoundInterval * 2f), Is.EqualTo(2), "the started burst still finishes");
            Assert.That(trigger.Tick(1f), Is.EqualTo(0), "no more bursts once released");
        }

        [Test]
        public void PressingDuringABurstDoesNotQueueAnother()
        {
            var trigger = TriggerModels.For(Trigger(FireMode.Semi));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(0));
            Assert.That(trigger.Release(), Is.EqualTo(0));
            Assert.That(trigger.Tick(RoundInterval * 2f), Is.EqualTo(2));
            Assert.That(trigger.Tick(FireInterval * 4f), Is.EqualTo(0));
        }

        [Test]
        public void ALongStepFiresTheRestOfTheBurstAtOnce()
        {
            var trigger = TriggerModels.For(Trigger(FireMode.Semi, rounds: 4));

            Assert.That(trigger.Press(), Is.EqualTo(1));
            Assert.That(trigger.Tick(RoundInterval * 3f), Is.EqualTo(3));
            Assert.That(trigger.Tick(FireInterval), Is.EqualTo(0));
            Assert.That(trigger.Press(), Is.EqualTo(1));
        }

        [Test]
        public void AZeroRoundIntervalFiresTheWholeBurstOnThePress()
        {
            var trigger = new BurstTrigger(new BurstSpec(3, 0f), new SemiTrigger(FireInterval));

            Assert.That(trigger.Press() + trigger.Tick(0f), Is.EqualTo(3));
            Assert.That(trigger.MinShotSpacingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void TheShortestSpacingIsTheRoundInterval()
        {
            Assert.That(TriggerModels.For(Trigger(FireMode.Semi)).MinShotSpacingSeconds, Is.EqualTo(RoundInterval));
            Assert.That(
                new BurstTrigger(new BurstSpec(3, 1f), new SemiTrigger(FireInterval)).MinShotSpacingSeconds,
                Is.EqualTo(FireInterval),
                "never slower than the inner trigger allows");
        }

        [Test]
        public void TheWeaponFiresEveryRoundOfTheBurst()
        {
            var weapon = new Weapon(Spec(FireMode.Semi));

            Assert.That(Fire(weapon, Pressed(weapon), RoundInterval, 8), Is.EqualTo(3));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(27));
        }

        [Test]
        public void TheBurstStopsWhenTheMagazineRunsOut()
        {
            var weapon = new Weapon(Spec(FireMode.Semi, magazineSize: 2));

            Assert.That(Fire(weapon, Pressed(weapon), RoundInterval, 8), Is.EqualTo(2));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(0));
        }

        [Test]
        public void TheServerBudgetAcceptsBurstsFiredAtTheirPace()
        {
            var budget = new ShotBudget(TriggerModels.For(Spec(FireMode.Auto)));

            foreach (var burstStart in new[] { 0d, FireInterval + (RoundInterval * 2f), 2d * (FireInterval + (RoundInterval * 2f)) })
            {
                for (var round = 0; round < 3; round++)
                {
                    Assert.That(budget.TryConsume(burstStart + (round * RoundInterval)), Is.True, "burst at " + burstStart + " round " + round);
                }
            }
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => new BurstTrigger(new BurstSpec(3, 0.1f), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => TriggerModels.For(Trigger(FireMode.Semi)).Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TriggerModels.For(Trigger(FireMode.Semi)).Tick(float.NaN));
        }

        private static int Pressed(Weapon weapon)
        {
            return weapon.Trigger.Press();
        }

        // 撃ち方が出した発数ぶん Weapon.TryFire を呼ぶ。オフラインの WeaponController と同じく、撃てなかった分は捨てる。
        private static int Fire(Weapon weapon, int firstShots, float step, int steps)
        {
            var fired = TryFire(weapon, firstShots);
            for (var i = 0; i < steps; i++)
            {
                weapon.Tick(step);
                fired += TryFire(weapon, weapon.Trigger.Tick(step));
            }

            return fired;
        }

        private static int TryFire(Weapon weapon, int shots)
        {
            var fired = 0;
            for (var i = 0; i < shots && weapon.TryFire(); i++)
            {
                fired++;
            }

            return fired;
        }
    }
}
