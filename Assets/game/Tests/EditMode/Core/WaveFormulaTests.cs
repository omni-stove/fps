using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WaveFormulaTests
    {
        [Test]
        public void FirstWaveUsesTheFirstValues()
        {
            var formula = new WaveFormula(3, 2f, 1f, 0.25f, 4, 5f);

            Assert.That(formula.At(1), Is.EqualTo(new Wave(3, 1f, 4, 5f)));
        }

        [Test]
        public void LaterWavesHaveMoreAndStrongerEnemies()
        {
            var formula = new WaveFormula(3, 2f, 1f, 0.25f, 4, 5f);

            var second = formula.At(2);
            Assert.That(second.EnemyCount, Is.EqualTo(5));
            Assert.That(second.StrengthMultiplier, Is.EqualTo(1.25f));
            Assert.That(second.MaxAlive, Is.EqualTo(4));
            Assert.That(second.RestSeconds, Is.EqualTo(5f));
            Assert.That(formula.At(5).EnemyCount, Is.EqualTo(11));
            Assert.That(formula.At(5).StrengthMultiplier, Is.EqualTo(2f));
        }

        [Test]
        public void FractionalGrowthRoundsHalfAwayFromZero()
        {
            var formula = new WaveFormula(1, 0.5f, 1f, 0f, 1, 0f);

            Assert.That(formula.At(1).EnemyCount, Is.EqualTo(1));
            Assert.That(formula.At(2).EnemyCount, Is.EqualTo(2), "1 + round(0.5)");
            Assert.That(formula.At(3).EnemyCount, Is.EqualTo(2), "1 + round(1.0)");
            Assert.That(formula.At(4).EnemyCount, Is.EqualTo(3), "1 + round(1.5)");
        }

        [Test]
        public void StrengthAndCountStopAtTheirCaps()
        {
            var formula = new WaveFormula(1, 1000f, 1f, 1f, 1, 0f, maxStrength: 2.5f);

            Assert.That(formula.At(2).StrengthMultiplier, Is.EqualTo(2f));
            Assert.That(formula.At(3).StrengthMultiplier, Is.EqualTo(2.5f));
            Assert.That(formula.At(100).StrengthMultiplier, Is.EqualTo(2.5f));
            Assert.That(formula.At(100).EnemyCount, Is.EqualTo(WaveFormula.MaxEnemyCount));
            Assert.That(formula.At(int.MaxValue).EnemyCount, Is.EqualTo(WaveFormula.MaxEnemyCount));
        }

        [Test]
        public void UncappedStrengthStaysFinite()
        {
            var formula = new WaveFormula(1, 0f, 1f, float.MaxValue, 1, 0f);

            Assert.That(float.IsInfinity(formula.At(int.MaxValue).StrengthMultiplier), Is.False);
        }

        [Test]
        public void AtRejectsNumbersBelowOne()
        {
            var formula = new WaveFormula(1, 0f, 1f, 0f, 1, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => formula.At(0));
        }

        [Test]
        public void ConstructorRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(0, 0f, 1f, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(WaveFormula.MaxEnemyCount + 1, 0f, 1f, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, -1f, 1f, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, float.NaN, 1f, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 0f, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 1f, -0.1f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 1f, 0f, 0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 1f, 0f, 1, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 1f, 0f, 1, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WaveFormula(1, 0f, 2f, 0f, 1, 0f, maxStrength: 1f));
        }

        [Test]
        public void WaveRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Wave(0, 1f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Wave(1, 0f, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Wave(1, float.PositiveInfinity, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Wave(1, 1f, 0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Wave(1, 1f, 1, float.NaN));
        }
    }
}
