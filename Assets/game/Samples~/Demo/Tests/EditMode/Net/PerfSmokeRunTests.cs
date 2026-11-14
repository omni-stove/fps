using System;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>試合を 120 秒回す本体は重いので <c>make perf</c> と nightly に任せ、ここではスイートとのつながりと設定だけを見る。</summary>
    public sealed class PerfSmokeRunTests
    {
        [Test]
        public void TheSuiteFindsThisRunByItsTypeName()
        {
            var entry = SmokeSuite.Runs.Single(run => run.Name == SmokeSuite.PerfSmokeRunName);

            Assert.That(entry.Name, Is.EqualTo(nameof(PerfSmokeRun)));
            Assert.That(entry.LogTag, Is.EqualTo(PerfSmokeRun.LogTag));
            Assert.That(Type.GetType(SmokeSuite.PerfSmokeRunTypeName), Is.EqualTo(typeof(PerfSmokeRun)));
            Assert.That(SmokeSuite.FindStaticRun(SmokeSuite.PerfSmokeRunTypeName).DeclaringType, Is.EqualTo(typeof(PerfSmokeRun)));
        }

        [Test]
        public void EveryNetEditorSmokeRunIsInTheSuite()
        {
            var smokeRuns = typeof(PerfSmokeRun).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(PerfSmokeRun).Namespace && type.Name.EndsWith("SmokeRun", StringComparison.Ordinal))
                .Select(type => type.Name);

            Assert.That(SmokeSuite.Runs.Select(run => run.Name), Is.SupersetOf(smokeRuns));
        }

        [Test]
        public void MeasuresSixteenBotsForTwoMinutesWithinTheIssueBudget()
        {
            var budget = new TickBudget();

            Assert.That(PerfSmokeRun.Bots, Is.EqualTo(16));
            Assert.That(PerfSmokeRun.MeasureSeconds, Is.EqualTo(120f));
            Assert.That(budget.AverageBudgetMs, Is.EqualTo(4d));
            Assert.That(budget.MaxBudgetMs, Is.EqualTo(12d));
            Assert.That(PerfSmokeRun.ReportPath.Replace('\\', '/'), Does.EndWith("/Logs/perf.json"));
        }

        [Test]
        public void TheMatchIsFilledToTheBotsPlusTheHost()
        {
            Assert.That(PerfSmokeRun.LaunchBotsFor(PerfSmokeRun.Bots, PerfSmokeRun.HostPlayers), Is.EqualTo(17));
            Assert.That(
                PerfSmokeRun.LaunchBotsFor(PerfSmokeRun.Bots, PerfSmokeRun.HostPlayers),
                Is.LessThanOrEqualTo(NetLaunchOptions.MaxBots),
                "NetTeamDeathmatch.LaunchBots rejects more than MaxBots");
            Assert.Throws<ArgumentOutOfRangeException>(() => PerfSmokeRun.LaunchBotsFor(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerfSmokeRun.LaunchBotsFor(1, -1));
        }

        [Test]
        public void TheWholeRunFitsInTheSuiteTimeout()
        {
            Assert.That(
                PerfSmokeRun.EnterTimeoutSeconds + PerfSmokeRun.BotFillTimeoutSeconds + PerfSmokeRun.WarmUpSeconds + PerfSmokeRun.MeasureSeconds,
                Is.LessThan(SmokeSuite.RunTimeoutSeconds));
        }
    }
}
