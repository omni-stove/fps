using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class HordeEconomyTests
    {
        private static readonly HordeSpec _spec = new HordeSpec(Side.Alpha, Side.Bravo, startingCurrency: 20, killReward: 10, killRewardPerWave: 5);

        private static WaveSchedule Schedule()
        {
            return WaveSchedule.FromFormula(new WaveFormula(1, 0f, 1f, 0f, 1, 3f), 3);
        }

        [Test]
        public void NoRewardIsPaidBeforeTheFirstWave()
        {
            var economy = new HordeEconomy(Schedule(), _spec);

            Assert.That(economy.TryEarn(out var reward), Is.False);
            Assert.That(reward, Is.Zero);
            Assert.That(economy.Currency, Is.EqualTo(20));
            Assert.That(economy.EnemiesKilled, Is.Zero);
        }

        [Test]
        public void TheRewardGrowsWithTheWave()
        {
            var schedule = Schedule();
            var economy = new HordeEconomy(schedule, _spec);
            schedule.Start();

            Assert.That(economy.TryEarn(out var first), Is.True);
            schedule.ReportSpawned();
            schedule.ReportKilled();
            schedule.Tick(3f);

            Assert.That(economy.TryEarn(out var second), Is.True);
            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(15));
            Assert.That(economy.Currency, Is.EqualTo(45));
            Assert.That(economy.EnemiesKilled, Is.EqualTo(2));
        }

        [Test]
        public void PurchasesWaitForTheRestAndTheCurrency()
        {
            var schedule = Schedule();
            var economy = new HordeEconomy(schedule, _spec);

            Assert.That(economy.Check(false, 5), Is.EqualTo(HordePurchaseVerdict.NotPlaying));
            Assert.That(economy.Check(true, 5), Is.EqualTo(HordePurchaseVerdict.Ok), "before the first wave counts as a rest");

            schedule.Start();
            Assert.That(economy.TryPay(true, 5), Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(economy.Currency, Is.EqualTo(20));

            schedule.ReportSpawned();
            schedule.ReportKilled();
            Assert.That(economy.TryPay(true, 25), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
            Assert.That(economy.TryPay(true, 15), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(economy.Currency, Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Check(true, -1));
        }

        [Test]
        public void PurchasesDuringAWaveCanBeAllowed()
        {
            var schedule = Schedule();
            var economy = new HordeEconomy(schedule, new HordeSpec(Side.Alpha, Side.Bravo, purchasesOnlyBetweenWaves: false));
            schedule.Start();

            Assert.That(economy.Check(true, 10), Is.EqualTo(HordePurchaseVerdict.Ok));
        }

        [Test]
        public void TheOfflineHordeUsesTheSameEconomy()
        {
            var schedule = Schedule();
            var horde = new Horde(schedule, _spec);

            Assert.That(horde.Currency, Is.EqualTo(20));
            Assert.That(horde.ReportEnemyKilled(), Is.Zero, "the session has not started");
            Assert.That(horde.CheckPurchase(5), Is.EqualTo(HordePurchaseVerdict.NotPlaying));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new HordeEconomy(null, _spec));
            Assert.Throws<ArgumentNullException>(() => _ = new HordeEconomy(Schedule(), null));
        }
    }
}
