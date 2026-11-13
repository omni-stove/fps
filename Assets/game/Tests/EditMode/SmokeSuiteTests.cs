using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    public sealed class SmokeSuiteTests
    {
        private static KeyValuePair<string, int> Result(string name, int code)
        {
            return new KeyValuePair<string, int>(name, code);
        }

        private static IReadOnlyList<KeyValuePair<string, int>> AllPassed()
        {
            return SmokeSuite.Runs.Select(run => Result(run.Name, SmokeSuite.SuccessExitCode)).ToList();
        }

        [Test]
        public void RunsEverySmokeRunOnceInTheIssueOrder()
        {
            Assert.That(
                SmokeSuite.Runs.Select(run => run.Name),
                Is.EqualTo(new[]
                {
                    nameof(CampaignSmokeRun),
                    nameof(DestructionSmokeRun),
                    nameof(EnemyFireSmokeRun),
                    nameof(EnemyStuckSmokeRun),
                    nameof(WeaponSwitchSmokeRun),
                    SmokeSuite.PerfSmokeRunName,
                }));
            Assert.That(SmokeSuite.Runs.Select(run => run.LogTag), Is.Unique);
        }

        // スイートに入れず、それぞれの -executeMethod で単独に走らせる SmokeRun。
        private static readonly string[] _standaloneSmokeRuns =
        {
            nameof(TpsActionSmokeRun),
            nameof(TpsRangeSmokeRun),
        };

        [Test]
        public void EveryEditorSmokeRunClassIsInTheSuite()
        {
            var smokeRuns = typeof(SmokeSuite).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(SmokeSuite).Namespace && type.Name.EndsWith("SmokeRun", StringComparison.Ordinal))
                .Select(type => type.Name)
                .Except(_standaloneSmokeRuns);

            // PerfSmokeRun は Fps.Net.Editor にあり、そちらの SmokeRun は Fps.Net.Tests の PerfSmokeRunTests が確かめる
            Assert.That(
                SmokeSuite.Runs.Select(run => run.Name).Where(name => name != SmokeSuite.PerfSmokeRunName),
                Is.EquivalentTo(smokeRuns));
        }

        [Test]
        public void FindStaticRunExplainsWhatIsMissing()
        {
            Assert.That(
                () => SmokeSuite.FindStaticRun("Fps.Editor.NoSuchSmokeRun, Fps.Editor"),
                Throws.InvalidOperationException.With.Message.Contains("was not found"));
            Assert.That(
                () => SmokeSuite.FindStaticRun(typeof(SmokeSuiteTests).AssemblyQualifiedName),
                Throws.InvalidOperationException.With.Message.Contains("has no public static Run()"));
            Assert.Throws<ArgumentException>(() => SmokeSuite.FindStaticRun(string.Empty));
            Assert.That(
                SmokeSuite.FindStaticRun(typeof(DestructionSmokeRun).AssemblyQualifiedName).Name,
                Is.EqualTo(nameof(DestructionSmokeRun.Run)));
        }

        [Test]
        public void TakeOverDoesNothingWhenTheSuiteIsNotRunning()
        {
            Assume.That(SmokeSuite.IsActive, Is.False);

            Assert.That(SmokeSuite.TakeOver("[Test]", SmokeSuite.SuccessExitCode), Is.False);
        }

        [Test]
        public void DetectsQuitInAnyCase()
        {
            Assert.That(SmokeSuite.HasQuitArgument(new[] { "Unity", "-batchmode", "-quit" }), Is.True);
            Assert.That(SmokeSuite.HasQuitArgument(new[] { "Unity", "-QUIT" }), Is.True);
            Assert.That(SmokeSuite.HasQuitArgument(new[] { "Unity", "-batchmode", "-executeMethod", "Fps.Editor.SmokeSuite.RunAll" }), Is.False);
            Assert.Throws<ArgumentNullException>(() => SmokeSuite.HasQuitArgument(null));
        }

        [Test]
        public void ResultsRoundTripInOrder()
        {
            var results = new[] { Result("A", 0), Result("B", 1), Result("C", 0) };

            var text = SmokeSuite.FormatResults(results);

            Assert.That(SmokeSuite.ParseResults(text), Is.EqualTo(results));
        }

        [Test]
        public void EmptyResultsParseToNothing()
        {
            Assert.That(SmokeSuite.ParseResults(null), Is.Empty);
            Assert.That(SmokeSuite.ParseResults(string.Empty), Is.Empty);
            Assert.That(SmokeSuite.FormatResults(Array.Empty<KeyValuePair<string, int>>()), Is.Empty);
        }

        [Test]
        public void MalformedResultsCountAsFailures()
        {
            var results = SmokeSuite.ParseResults("A=0;B;C=x;=0");

            Assert.That(results, Is.EqualTo(new[]
            {
                Result("A", SmokeSuite.SuccessExitCode),
                Result("B", SmokeSuite.FailureExitCode),
                Result("C=x", SmokeSuite.FailureExitCode),
                Result("=0", SmokeSuite.FailureExitCode),
            }));
        }

        [TestCase("")]
        [TestCase("A;B")]
        [TestCase("A=B")]
        public void FormatRejectsNamesThatWouldNotParseBack(string name)
        {
            Assert.Throws<ArgumentException>(() => SmokeSuite.FormatResults(new[] { Result(name, 0) }));
        }

        [Test]
        public void PassesOnlyWhenEveryRunPassed()
        {
            Assert.That(SmokeSuite.ExitCodeFor(AllPassed(), SmokeSuite.Runs), Is.EqualTo(SmokeSuite.SuccessExitCode));
        }

        [Test]
        public void FailsWhenAnyRunFailed()
        {
            var results = AllPassed().ToList();
            results[2] = Result(results[2].Key, SmokeSuite.FailureExitCode);

            Assert.That(SmokeSuite.ExitCodeFor(results, SmokeSuite.Runs), Is.EqualTo(SmokeSuite.FailureExitCode));
        }

        [Test]
        public void FailsWhenARunHasNoResult()
        {
            var results = AllPassed().Skip(1).ToList();

            Assert.That(SmokeSuite.ExitCodeFor(results, SmokeSuite.Runs), Is.EqualTo(SmokeSuite.FailureExitCode));
        }

        [Test]
        public void EntryRejectsMissingParts()
        {
            Assert.Throws<ArgumentException>(() => new SmokeRunEntry(string.Empty, "[X]", () => { }));
            Assert.Throws<ArgumentNullException>(() => new SmokeRunEntry("X", null, () => { }));
            Assert.Throws<ArgumentNullException>(() => new SmokeRunEntry("X", "[X]", null));
        }
    }
}
