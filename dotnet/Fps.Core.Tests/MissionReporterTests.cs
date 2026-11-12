using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MissionReporterTests
    {
        [Test]
        public void FormatsObjectiveLine()
        {
            Assert.That(
                MissionReporter.FormatObjective("reach_area", ObjectiveState.Active),
                Is.EqualTo("[Mission] objective=reach_area state=Active"));
            Assert.That(MissionReporter.CompletedLine, Is.EqualTo("[Mission] completed"));
            Assert.That(MissionReporter.FailedLine, Is.EqualTo("[Mission] failed"));
        }

        [Test]
        public void RejectsNullArguments()
        {
            var mission = TwoStep();

            Assert.Throws<ArgumentNullException>(() => new MissionReporter(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new MissionReporter(mission, null));
            Assert.Throws<ArgumentNullException>(() => MissionReporter.FormatObjective(null, ObjectiveState.Active));
        }

        [Test]
        public void ReportAllListsInitialStatesInDefinitionOrder()
        {
            var lines = new List<string>();
            using var reporter = new MissionReporter(TwoStep(), lines.Add);

            reporter.ReportAll();

            Assert.That(lines, Is.EqualTo(_initialLines));
        }

        [Test]
        public void ReportsChangesAndCompletion()
        {
            var mission = TwoStep();
            var lines = new List<string>();
            using var reporter = new MissionReporter(mission, lines.Add);

            mission.Progress("a");
            mission.Progress("b");

            Assert.That(lines, Is.EqualTo(_completionLines));
        }

        [Test]
        public void ReportsFailure()
        {
            var mission = TwoStep();
            var lines = new List<string>();
            using var reporter = new MissionReporter(mission, lines.Add);

            mission.Fail("a");

            Assert.That(lines, Is.EqualTo(_failureLines));
        }

        [Test]
        public void ReportAllIncludesTheResultOfAFinishedMission()
        {
            var mission = TwoStep();
            mission.Fail("a");
            var lines = new List<string>();
            using var reporter = new MissionReporter(mission, lines.Add);

            reporter.ReportAll();

            Assert.That(lines[lines.Count - 1], Is.EqualTo("[Mission] failed"));
        }

        [Test]
        public void StopsReportingAfterDispose()
        {
            var mission = TwoStep();
            var lines = new List<string>();
            var reporter = new MissionReporter(mission, lines.Add);

            reporter.Dispose();
            reporter.Dispose();
            mission.Progress("a");

            Assert.That(lines, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => reporter.ReportAll());
        }

        private static readonly string[] _prerequisiteA = { "a" };

        private static readonly string[] _initialLines =
        {
            "[Mission] objective=a state=Active",
            "[Mission] objective=b state=Locked",
        };

        private static readonly string[] _completionLines =
        {
            "[Mission] objective=a state=Completed",
            "[Mission] objective=b state=Active",
            "[Mission] objective=b state=Completed",
            "[Mission] completed",
        };

        private static readonly string[] _failureLines =
        {
            "[Mission] objective=a state=Failed",
            "[Mission] failed",
        };

        private static Mission TwoStep()
        {
            return new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: _prerequisiteA),
            });
        }
    }
}
