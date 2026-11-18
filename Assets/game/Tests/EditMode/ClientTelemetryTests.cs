using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class ClientTelemetryTests
    {
        private const string FileName = "crash-test.jsonl";

        private readonly List<ClientTelemetry> _created = new List<ClientTelemetry>();
        private ControlSettings _appliedBefore;
        private string _folder;

        private string FilePath => Path.Combine(_folder, FileName);

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _folder = Path.Combine(Path.GetTempPath(), "shooter-client-telemetry-" + Guid.NewGuid().ToString("N"));
            GameSignals.ModeStatus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var telemetry in _created)
            {
                telemetry.Dispose();
            }

            _created.Clear();
            GameSignals.ModeStatus.Clear();
            AppliedControlSettings.Apply(_appliedBefore);
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void ALoggedExceptionWithConsentLeavesOneLine()
        {
            var telemetry = Attached(consent: true);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Debug.LogException(new InvalidOperationException("boom"));
            telemetry.Dispose();

            var lines = ReadLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            var values = Parse(lines[0]);
            Assert.That(values["type"], Is.EqualTo("crash"));
            Assert.That(values["source"], Is.EqualTo("client"));
            Assert.That(values["exception"], Is.EqualTo("InvalidOperationException"));
            Assert.That(values["message"], Is.EqualTo("boom"));
            Assert.That(values.TryGetValue("version", out var version) ? version : string.Empty, Is.EqualTo(Application.version.Trim()));
            Assert.That(values["session"], Does.Match("^[0-9a-f]{32}$"));
        }

        [Test]
        public void ALoggedExceptionWithoutConsentLeavesNothing()
        {
            var telemetry = Attached(consent: false);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Debug.LogException(new InvalidOperationException("boom"));
            telemetry.Dispose();

            Assert.That(File.Exists(FilePath), Is.False);
            Assert.That(Directory.Exists(_folder), Is.False, "not even the folder is made");
        }

        [Test]
        public void WithdrawingConsentInTheSettingsStopsTheNextCrash()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithTelemetryConsent(true));
            var telemetry = Attached(consent: true);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: first");
            Debug.LogException(new InvalidOperationException("first"));
            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithTelemetryConsent(false));
            LogAssert.Expect(LogType.Exception, "FormatException: second");
            Debug.LogException(new FormatException("second"));
            telemetry.Dispose();

            Assert.That(telemetry.Recorder.Consent, Is.False);
            var lines = ReadLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            Assert.That(Parse(lines[0])["message"], Is.EqualTo("first"));
        }

        [Test]
        public void GivingConsentInTheSettingsStartsRecording()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithTelemetryConsent(false));
            var telemetry = Attached(consent: false);

            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithTelemetryConsent(true));
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Debug.LogException(new InvalidOperationException("boom"));
            telemetry.Dispose();

            Assert.That(ReadLines(), Has.Length.EqualTo(1));
        }

        [Test]
        public void WarningsAndErrorsAreNotCrashes()
        {
            var telemetry = Created(consent: true);

            Assert.That(telemetry.Crashes.Capture("something odd", "at A()", LogType.Warning), Is.False);
            Assert.That(telemetry.Crashes.Capture("something wrong", "at A()", LogType.Error), Is.False);
            Assert.That(telemetry.Crashes.Capture("NullReferenceException: x", "at A()", LogType.Exception), Is.True);
        }

        [Test]
        public void DetachingStopsCapturing()
        {
            var telemetry = Attached(consent: true);
            telemetry.Detach();

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Debug.LogException(new InvalidOperationException("boom"));
            telemetry.Dispose();

            Assert.That(File.Exists(FilePath), Is.False);
            Assert.That(telemetry.Crashes.IsAttached, Is.False);
        }

        [TestCase("InvalidOperationException: boom", "InvalidOperationException", "boom")]
        [TestCase("Shooter.Core.TuningJsonException: a: b", "Shooter.Core.TuningJsonException", "a: b")]
        [TestCase("NullReferenceException", "NullReferenceException", "")]
        [TestCase("Something failed: badly", "", "Something failed: badly")]
        [TestCase("", "", "")]
        public void TheConditionSplitsIntoTypeAndMessage(string condition, string type, string message)
        {
            CrashCapture.SplitCondition(condition, out var actualType, out var actualMessage);

            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualMessage, Is.EqualTo(message));
        }

        [Test]
        public void TheEndOfAMatchWritesOneSummary()
        {
            var now = 10f;
            var telemetry = Created(consent: true);
            var reporter = new MatchSummaryReporter(telemetry.Recorder, () => now, () => "Domination", () => "Yard", () => "local-1");
            reporter.Attach();
            try
            {
                GameSignals.ModeStatus.Publish(Status(ended: false, winner: null));
                now = 95.5f;
                GameSignals.ModeStatus.Publish(Status(ended: true, winner: Side.Bravo));
                GameSignals.ModeStatus.Publish(Status(ended: true, winner: Side.Bravo));
            }
            finally
            {
                reporter.Dispose();
            }

            telemetry.Dispose();

            Assert.That(reporter.Reported, Is.EqualTo(1));
            var lines = ReadLines();
            Assert.That(lines, Has.Length.EqualTo(1));
            var values = Parse(lines[0]);
            Assert.That(values["type"], Is.EqualTo("matchSummary"));
            Assert.That(values["matchId"], Is.EqualTo("local-1"));
            Assert.That(values["mode"], Is.EqualTo("Domination"));
            Assert.That(values["map"], Is.EqualTo("Yard"));
            Assert.That(values["durationSeconds"], Is.EqualTo("85.5"));
            Assert.That(values["winner"], Is.EqualTo("1"));
            Assert.That(values["ownSide"], Is.EqualTo("0"));
            Assert.That(values["result"], Is.EqualTo("lost"));
            Assert.That(values["score0"], Is.EqualTo("40"));
            Assert.That(values["score1"], Is.EqualTo("100"));
        }

        [Test]
        public void AMatchWhoseStartWasNotSeenIsNotSummarized()
        {
            var telemetry = Created(consent: true);
            var reporter = new MatchSummaryReporter(telemetry.Recorder, () => 0f);

            reporter.Observe(Status(ended: true, winner: Side.Alpha));
            telemetry.Dispose();

            Assert.That(reporter.Reported, Is.Zero);
            Assert.That(File.Exists(FilePath), Is.False);
        }

        [Test]
        public void ARematchAfterTheEndIsSummarizedAgain()
        {
            var now = 0f;
            var telemetry = Created(consent: true);
            var reporter = new MatchSummaryReporter(telemetry.Recorder, () => now);

            reporter.Observe(Status(ended: false, winner: null));
            now = 30f;
            reporter.Observe(Status(ended: true, winner: null));
            now = 40f;
            reporter.Observe(Status(ended: false, winner: null));
            now = 100f;
            reporter.Observe(Status(ended: true, winner: Side.Alpha));
            telemetry.Dispose();

            Assert.That(reporter.Reported, Is.EqualTo(2));
            var lines = ReadLines();
            Assert.That(Parse(lines[0])["durationSeconds"], Is.EqualTo("30"));
            Assert.That(Parse(lines[0])["result"], Is.EqualTo("draw"));
            Assert.That(Parse(lines[1])["durationSeconds"], Is.EqualTo("60"));
            Assert.That(Parse(lines[1])["result"], Is.EqualTo("won"));
        }

        [Test]
        public void WithoutConsentTheSummaryIsNotWritten()
        {
            var now = 0f;
            var telemetry = Created(consent: false);
            var reporter = new MatchSummaryReporter(telemetry.Recorder, () => now);

            reporter.Observe(Status(ended: false, winner: null));
            now = 30f;
            reporter.Observe(Status(ended: true, winner: Side.Alpha));
            telemetry.Dispose();

            Assert.That(reporter.Reported, Is.Zero);
            Assert.That(File.Exists(FilePath), Is.False);
        }

        [Test]
        public void TheBundledSpecIsReadThroughKitTuning()
        {
            KitTuning.Reload();
            try
            {
                var spec = ClientTelemetry.LoadSpec();

                Assert.That(spec.Sink, Is.EqualTo(TelemetrySpec.DefaultSink));
                Assert.That(spec.FileName, Is.EqualTo(TelemetrySpec.DefaultFileName));
            }
            finally
            {
                KitTuning.Reload();
            }
        }

        private ClientTelemetry Created(bool consent)
        {
            var telemetry = ClientTelemetry.Create(new TelemetrySpec(fileName: FileName), _folder, consent);
            _created.Add(telemetry);
            return telemetry;
        }

        private ClientTelemetry Attached(bool consent)
        {
            var telemetry = Created(consent);
            telemetry.Attach();
            return telemetry;
        }

        private static ModeStatus Status(bool ended, Side? winner)
        {
            var scores = new[] { new SideScore(Side.Alpha, 40), new SideScore(Side.Bravo, 100) };
            return new ModeStatus(null, scores, Side.Alpha, null, ended, winner, null);
        }

        private string[] ReadLines()
        {
            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                return text.Length == 0 ? Array.Empty<string>() : text.TrimEnd('\n').Split('\n');
            }
        }

        private static IReadOnlyDictionary<string, string> Parse(string line)
        {
            Assert.That(FlatJsonObject.TryParse(line, out var values, out var error), Is.True, error);
            return values;
        }
    }
}
