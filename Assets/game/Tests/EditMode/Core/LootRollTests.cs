using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LootRollTests
    {
        private const string _bandage = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";
        private const string _medkit = "61dd226a-156c-4060-ab8f-5dbed8cb7f4f";

        private static LootTable Table(int rolls = 1)
        {
            return new LootTable(
                "medical",
                new[] { new LootEntry(_bandage, 3f, 1, 4), new LootEntry(_medkit, 1f) },
                rolls);
        }

        [Test]
        public void PickChoosesTheRowWhoseShareOfTheWeightHoldsTheFraction()
        {
            var table = Table();

            Assert.That(LootRoll.Pick(table, 0.0).ItemId, Is.EqualTo(_bandage));
            Assert.That(LootRoll.Pick(table, 0.74).ItemId, Is.EqualTo(_bandage));
            Assert.That(LootRoll.Pick(table, 0.75).ItemId, Is.EqualTo(_medkit));
            Assert.That(LootRoll.Pick(table, 0.999).ItemId, Is.EqualTo(_medkit));
        }

        [Test]
        public void PickRejectsAFractionOutsideZeroToOne()
        {
            var table = Table();

            Assert.Throws<ArgumentOutOfRangeException>(() => LootRoll.Pick(table, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootRoll.Pick(table, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootRoll.Pick(table, double.NaN));
            Assert.Throws<ArgumentNullException>(() => LootRoll.Pick(null, 0.5));
        }

        [Test]
        public void CountOfSpreadsTheFractionEvenlyFromMinToMax()
        {
            var bandage = Table().Entries[0];

            Assert.That(LootRoll.CountOf(bandage, 0.0), Is.EqualTo(1));
            Assert.That(LootRoll.CountOf(bandage, 0.24), Is.EqualTo(1));
            Assert.That(LootRoll.CountOf(bandage, 0.25), Is.EqualTo(2));
            Assert.That(LootRoll.CountOf(bandage, 0.5), Is.EqualTo(3));
            Assert.That(LootRoll.CountOf(bandage, 0.999), Is.EqualTo(4));
            Assert.That(LootRoll.CountOf(Table().Entries[1], 0.999), Is.EqualTo(1));
        }

        [Test]
        public void CountOfDoesNotOverflowAtTheLargestMaxCount()
        {
            var entry = new LootEntry(_bandage, 1f, 1, int.MaxValue);

            Assert.That(LootRoll.CountOf(entry, 0.0), Is.EqualTo(1));
            Assert.That(LootRoll.CountOf(entry, 0.9999999999), Is.InRange(1, int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootRoll.CountOf(entry, 1.0));
        }

        [Test]
        public void RollDrawsOncePerRollAndMergesTheSameItem()
        {
            var random = new FixedRandom(0.1, 0.5, 0.9, 0.0, 0.2, 0.99);

            var items = LootRoll.Roll(Table(3), random);

            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].ItemId, Is.EqualTo(_bandage));
            Assert.That(items[0].Count, Is.EqualTo(7));
            Assert.That(items[1].ItemId, Is.EqualTo(_medkit));
            Assert.That(items[1].Count, Is.EqualTo(1));
            Assert.That(random.Used, Is.EqualTo(6));
        }

        [Test]
        public void RollWithASeededRandomStaysInsideTheTable()
        {
            var random = new Random(1234);
            for (var i = 0; i < 100; i++)
            {
                foreach (var item in LootRoll.Roll(Table(2), random))
                {
                    Assert.That(item.ItemId, Is.EqualTo(_bandage).Or.EqualTo(_medkit));
                    Assert.That(item.Count, Is.InRange(1, 8));
                }
            }
        }

        [Test]
        public void RollRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => LootRoll.Roll(null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => LootRoll.Roll(Table(), null));
        }
    }
}
