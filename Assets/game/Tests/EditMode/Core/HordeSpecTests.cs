using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HordeSpecTests
    {
        [Test]
        public void TheDefaultsAreTheKitValues()
        {
            var spec = new HordeSpec(Side.Alpha, Side.Bravo);

            Assert.That(HordeSpec.DefaultEnemySideId, Is.EqualTo(1));
            Assert.That(spec.StartingCurrency, Is.EqualTo(50));
            Assert.That(spec.KillReward, Is.EqualTo(10));
            Assert.That(spec.KillRewardPerWave, Is.EqualTo(2));
            Assert.That(spec.PurchasesOnlyBetweenWaves, Is.True);
            Assert.That(spec.BuildCost, Is.EqualTo(40));
            Assert.That(spec.RepairCostPerPiece, Is.EqualTo(4));
            Assert.That(spec.InteractRadius, Is.EqualTo(3.5f));
            Assert.That(spec.KillScore, Is.EqualTo(1));
            Assert.That(spec.AlertInterval, Is.EqualTo(3f));
            Assert.That(spec.MinSpawnDistance, Is.EqualTo(8f));
            Assert.That(spec.CorpseSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void TheConfiguredValuesAreKept()
        {
            var spec = new HordeSpec(
                Side.Alpha,
                new Side(3),
                buildCost: 10,
                repairCostPerPiece: 2,
                interactRadius: 6f,
                killScore: 5,
                alertInterval: 1.5f,
                minSpawnDistance: 0f,
                corpseSeconds: 0f);

            Assert.That(spec.Attackers, Is.EqualTo(new Side(3)));
            Assert.That(spec.BuildCost, Is.EqualTo(10));
            Assert.That(spec.RepairCostPerPiece, Is.EqualTo(2));
            Assert.That(spec.InteractRadius, Is.EqualTo(6f));
            Assert.That(spec.KillScore, Is.EqualTo(5));
            Assert.That(spec.AlertInterval, Is.EqualTo(1.5f));
            Assert.That(spec.MinSpawnDistance, Is.EqualTo(0f));
            Assert.That(spec.CorpseSeconds, Is.EqualTo(0f), "0 keeps the bodies");
        }

        [Test]
        public void TheSpecRejectsBadFortificationAndEnemyValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, buildCost: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, repairCostPerPiece: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, interactRadius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, interactRadius: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, interactRadius: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, killScore: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, alertInterval: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, minSpawnDistance: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, corpseSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, corpseSeconds: float.NaN));
        }

        [Test]
        public void TheRepairCostIsPerPieceAndSaturates()
        {
            var spec = new HordeSpec(Side.Alpha, Side.Bravo, repairCostPerPiece: 3);

            Assert.That(spec.RepairCost(0), Is.EqualTo(0));
            Assert.That(spec.RepairCost(4), Is.EqualTo(12));
            Assert.That(new HordeSpec(Side.Alpha, Side.Bravo, repairCostPerPiece: int.MaxValue).RepairCost(2), Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.RepairCost(-1));
        }

        [Test]
        public void ALowerBuildCostCanBeBoughtWithLessCurrency()
        {
            var cheap = Playing(new HordeSpec(Side.Alpha, Side.Bravo, startingCurrency: 10, buildCost: 10));
            Assert.That(cheap.CheckPurchase(cheap.Spec.BuildCost), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(cheap.TryPurchase(cheap.Spec.BuildCost), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(cheap.Currency, Is.EqualTo(0));

            var standard = Playing(new HordeSpec(Side.Alpha, Side.Bravo, startingCurrency: 10));
            Assert.That(standard.CheckPurchase(standard.Spec.BuildCost), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency), "the default 40 is out of reach");
            Assert.That(standard.TryPurchase(standard.Spec.BuildCost), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
            Assert.That(standard.Currency, Is.EqualTo(10));
        }

        [Test]
        public void TheRepairCostDecidesWhetherARepairCanBeBought()
        {
            var horde = Playing(new HordeSpec(Side.Alpha, Side.Bravo, startingCurrency: 9, repairCostPerPiece: 3));

            Assert.That(horde.CheckPurchase(horde.Spec.RepairCost(3)), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(horde.CheckPurchase(horde.Spec.RepairCost(4)), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
        }

        [Test]
        public void EachKillScoresTheConfiguredPoints()
        {
            var schedule = WaveSchedule.FromTable(new[] { new Wave(2, 1f, 2, 5f), new Wave(2, 1f, 2, 5f) });
            var horde = new Horde(schedule, new HordeSpec(Side.Alpha, Side.Bravo, killScore: 3));
            horde.Session.AddPlayer(0, Side.Alpha);
            horde.Session.Start();
            schedule.Start();

            schedule.ReportSpawned();
            schedule.ReportKilled();
            horde.ReportEnemyKilled();

            Assert.That(horde.Session.GetScore(Side.Alpha), Is.EqualTo(3d));
            Assert.That(horde.EnemiesKilled, Is.EqualTo(1), "the kill count stays one per enemy");
        }

        private static Horde Playing(HordeSpec spec)
        {
            var horde = new Horde(WaveSchedule.FromTable(new[] { new Wave(2, 1f, 2, 5f) }), spec);
            horde.Session.AddPlayer(0, Side.Alpha);
            horde.Session.Start();
            return horde;
        }
    }
}
