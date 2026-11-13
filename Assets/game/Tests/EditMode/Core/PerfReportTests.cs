using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PerfReportTests
    {
        [Test]
        public void WritesTheStatisticsAndThePassingVerdict()
        {
            var budget = new TickBudget();
            budget.Record(1d);
            budget.Record(2d);

            var json = PerfReport.ToJson("NetArena01", 16, 120d, budget, null);

            Assert.That(json, Is.EqualTo(
                "{\n"
                + "  \"scene\": \"NetArena01\",\n"
                + "  \"bots\": 16,\n"
                + "  \"measuredSeconds\": 120,\n"
                + "  \"ticks\": 2,\n"
                + "  \"averageMs\": 1.5,\n"
                + "  \"maxMs\": 2,\n"
                + "  \"p95Ms\": 2,\n"
                + "  \"averageBudgetMs\": 4,\n"
                + "  \"maxBudgetMs\": 12,\n"
                + "  \"passed\": true,\n"
                + "  \"violations\": []\n"
                + "}\n"));
        }

        [Test]
        public void ListsTheBudgetViolationsAndOtherProblems()
        {
            var budget = new TickBudget(0.01d, 0.01d);
            budget.Record(0.5d);

            var json = PerfReport.ToJson("NetArena01", 16, 120d, budget, new[] { "Error: \"boom\"" });

            Assert.That(json, Does.Contain("\"passed\": false,"));
            Assert.That(json, Does.Contain("\"average 0.5 ms exceeds the budget of 0.01 ms\""));
            Assert.That(json, Does.Contain("\"max 0.5 ms exceeds the budget of 0.01 ms\""));
            Assert.That(json, Does.Contain("\"Error: \\\"boom\\\"\"\n  ]\n}\n"));
        }

        [Test]
        public void AProblemAloneFailsEvenWithinTheBudget()
        {
            var budget = new TickBudget();
            budget.Record(1d);

            var json = PerfReport.ToJson("NetArena01", 16, 120d, budget, new[] { "only 3 of 16 bots joined" });

            Assert.That(json, Does.Contain("\"passed\": false,"));
        }

        [Test]
        public void NothingMeasuredFails()
        {
            var json = PerfReport.ToJson("NetArena01", 0, 0d, new TickBudget(), null);

            Assert.That(json, Does.Contain("\"ticks\": 0,"));
            Assert.That(json, Does.Contain("\"passed\": false,"));
            Assert.That(json, Does.Contain("\"no ticks were recorded\""));
        }

        [Test]
        public void QuotesEscapeJsonSpecialCharacters()
        {
            Assert.That(PerfReport.Quote("a\"b\\c\nd\te\u0001"), Is.EqualTo("\"a\\\"b\\\\c\\nd\\te\\u0001\""));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var budget = new TickBudget();

            Assert.Throws<ArgumentNullException>(() => PerfReport.ToJson(null, 0, 0d, budget, null));
            Assert.Throws<ArgumentNullException>(() => PerfReport.ToJson("A", 0, 0d, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerfReport.ToJson("A", -1, 0d, budget, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerfReport.ToJson("A", 0, double.NaN, budget, null));
            Assert.Throws<ArgumentNullException>(() => PerfReport.Quote(null));
        }
    }
}
