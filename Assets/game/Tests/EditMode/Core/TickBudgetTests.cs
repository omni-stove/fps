using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TickBudgetTests
    {
        private static TickBudget Recorded(TickBudget budget, params double[] samples)
        {
            foreach (var sample in samples)
            {
                budget.Record(sample);
            }

            return budget;
        }

        [Test]
        public void TheDefaultBudgetIsFourMillisecondsOnAverageAndTwelveAtMost()
        {
            var budget = new TickBudget();

            Assert.That(budget.AverageBudgetMs, Is.EqualTo(4d));
            Assert.That(budget.MaxBudgetMs, Is.EqualTo(12d));
        }

        [Test]
        public void AggregatesTheAverageAndTheMax()
        {
            var budget = Recorded(new TickBudget(), 1d, 3d, 2d, 6d);

            Assert.That(budget.Count, Is.EqualTo(4));
            Assert.That(budget.AverageMs, Is.EqualTo(3d).Within(1e-9));
            Assert.That(budget.MaxMs, Is.EqualTo(6d));
        }

        [Test]
        public void TheP95IsTheNearestRank()
        {
            var budget = new TickBudget();
            for (var i = 20; i >= 1; i--)
            {
                budget.Record(i);
            }

            Assert.That(budget.P95Ms, Is.EqualTo(19d), "ceil(0.95 * 20) = 19th smallest of 1..20");
        }

        [Test]
        public void TheP95OfOneSampleIsThatSample()
        {
            Assert.That(Recorded(new TickBudget(), 2.5d).P95Ms, Is.EqualTo(2.5d));
        }

        [Test]
        public void TheP95PicksUpRareSpikesThatTheAverageHides()
        {
            var budget = new TickBudget();
            for (var i = 0; i < 90; i++)
            {
                budget.Record(1d);
            }

            for (var i = 0; i < 10; i++)
            {
                budget.Record(10d);
            }

            Assert.That(budget.P95Ms, Is.EqualTo(10d));
            Assert.That(budget.AverageMs, Is.EqualTo(1.9d).Within(1e-9));
        }

        [Test]
        public void AnEmptyBudgetReportsZerosAndFails()
        {
            var budget = new TickBudget();

            Assert.That(budget.Count, Is.EqualTo(0));
            Assert.That(budget.AverageMs, Is.EqualTo(0d));
            Assert.That(budget.MaxMs, Is.EqualTo(0d));
            Assert.That(budget.P95Ms, Is.EqualTo(0d));
            Assert.That(budget.IsWithinBudget, Is.False, "nothing measured must not pass");
            Assert.That(budget.Violations(), Has.Count.EqualTo(1));
        }

        [Test]
        public void PassesWithinTheBudget()
        {
            var budget = Recorded(new TickBudget(), 1d, 2d, 11d);

            Assert.That(budget.IsWithinBudget, Is.True);
            Assert.That(budget.Violations(), Is.Empty);
        }

        [Test]
        public void ExactlyTheBudgetStillPasses()
        {
            var budget = Recorded(new TickBudget(4d, 12d), 4d, 4d);
            var spike = Recorded(new TickBudget(4d, 12d), 12d, 0d, 0d);

            Assert.That(budget.IsWithinBudget, Is.True);
            Assert.That(spike.IsWithinBudget, Is.True);
        }

        [Test]
        public void FailsWhenTheAverageIsOverTheBudget()
        {
            var budget = Recorded(new TickBudget(), 5d, 5d);

            Assert.That(budget.IsWithinBudget, Is.False);
            Assert.That(budget.Violations(), Has.Count.EqualTo(1));
            Assert.That(budget.Violations()[0], Does.StartWith("average 5 ms"));
        }

        [Test]
        public void FailsWhenOneTickIsOverTheMax()
        {
            var budget = Recorded(new TickBudget(), 1d, 1d, 1d, 1d, 12.5d);

            Assert.That(budget.AverageMs, Is.LessThan(budget.AverageBudgetMs));
            Assert.That(budget.IsWithinBudget, Is.False);
            Assert.That(budget.Violations(), Has.Count.EqualTo(1));
            Assert.That(budget.Violations()[0], Does.StartWith("max 12.5 ms"));
        }

        [Test]
        public void ATinyBudgetFailsOrdinaryTicks()
        {
            var budget = Recorded(new TickBudget(0.01d, 0.01d), 0.5d, 0.8d, 1.2d);

            Assert.That(budget.IsWithinBudget, Is.False);
            Assert.That(budget.Violations(), Has.Count.EqualTo(2));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void BudgetsMustBePositiveAndFinite(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickBudget(value, 12d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickBudget(4d, value));
        }

        [TestCase(-0.001d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TickTimesMustBeFiniteAndNotNegative(double value)
        {
            var budget = new TickBudget();

            Assert.Throws<ArgumentOutOfRangeException>(() => budget.Record(value));
            Assert.That(budget.Count, Is.EqualTo(0), "a rejected sample is not counted");
        }

        [Test]
        public void ZeroIsAValidTickTime()
        {
            var budget = Recorded(new TickBudget(), 0d);

            Assert.That(budget.Count, Is.EqualTo(1));
            Assert.That(budget.IsWithinBudget, Is.True);
        }

        [Test]
        public void TheLogLineUsesTheInvariantCulture()
        {
            var budget = Recorded(new TickBudget(), 1.25d, 2.75d);

            Assert.That(budget.ToString(), Is.EqualTo("ticks=2 avg=2ms max=2.75ms p95=2.75ms (budget avg=4ms max=12ms)"));
        }
    }
}
