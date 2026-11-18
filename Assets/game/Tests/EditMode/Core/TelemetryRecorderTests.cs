using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TelemetryRecorderTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TelemetryOrigin _origin = new TelemetryOrigin("client", "session-1", "1.0.0", "Test");
        private static readonly bool[] _flushedWithoutWaiting = { false };
        private static readonly bool[] _flushedAndWaited = { true };

        private List<string> _log;
        private List<RecordingSink> _sinks;
        private double _roll;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _sinks = new List<RecordingSink>();
            _roll = 0d;
        }

        [Test]
        public void WithoutConsentNothingIsRecordedAndNoSinkIsMade()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: false))
            {
                Assert.That(recorder.RecordCrash("InvalidOperationException", "boom", "at A()"), Is.False);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.False);
                recorder.Tick(60f);

                Assert.That(recorder.IsRecording, Is.False);
                Assert.That(recorder.HasSink, Is.False);
                Assert.That(_sinks, Is.Empty);
                Assert.That(_log, Does.Contain("[Telemetry] not recording: no consent"));
            }
        }

        [Test]
        public void WithConsentACrashIsOneLineFlushedAtOnce()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                Assert.That(recorder.RecordCrash("InvalidOperationException", "boom", "at A()"), Is.True);

                Assert.That(_sinks, Has.Count.EqualTo(1));
                Assert.That(_sinks[0].Lines, Has.Count.EqualTo(1));
                Assert.That(_sinks[0].Lines[0], Does.StartWith("{\"type\":\"crash\",\"time\":\"2026-11-18T09:00:00.000Z\""));
                Assert.That(_sinks[0].Lines[0], Does.Contain("\"exception\":\"InvalidOperationException\",\"message\":\"boom\""));
                Assert.That(_sinks[0].Flushes, Is.EqualTo(_flushedWithoutWaiting));
                Assert.That(_log, Does.Contain("[Telemetry] recording to telemetry.jsonl (sink=LocalFile)"));
            }
        }

        [Test]
        public void ACrashFromAnExceptionKeepsTheFullTypeAndTheInnerException()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                var exception = new InvalidOperationException("outer", new FormatException("inner"));

                Assert.That(recorder.RecordCrash(exception, waitForDelivery: true), Is.True);

                Assert.That(FlatJsonObject.TryParse(_sinks[0].Lines[0], out var values, out _), Is.True);
                Assert.That(values["exception"], Is.EqualTo("System.InvalidOperationException"));
                Assert.That(values["message"], Is.EqualTo("outer"));
                Assert.That(values["stackTrace"], Does.Contain("System.FormatException: inner"));
                Assert.That(_sinks[0].Flushes, Is.EqualTo(_flushedAndWaited));
            }
        }

        [Test]
        public void WithdrawingConsentDropsTheSinkAndStopsRecording()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                recorder.RecordCrash("E", "first", "at A()");

                recorder.SetConsent(false);
                System.Threading.SpinWait.SpinUntil(() => _sinks[0].Disposed, 5000);

                Assert.That(_sinks[0].Disposed, Is.True);
                Assert.That(_sinks[0].Flushes, Has.Count.EqualTo(1), "lines left after withdrawing are not sent");
                Assert.That(recorder.RecordCrash("E", "second", "at B()"), Is.False);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.False);
                Assert.That(_sinks, Has.Count.EqualTo(1));
                Assert.That(_log, Does.Contain("[Telemetry] consent withdrawn; nothing more is recorded or sent"));
            }
        }

        [Test]
        public void GivingConsentLaterStartsRecordingWithANewSink()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: false))
            {
                recorder.SetConsent(true);

                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.True);
                Assert.That(_sinks, Has.Count.EqualTo(1));
                Assert.That(_log, Does.Contain("[Telemetry] consent given; recording from now on"));
            }
        }

        [Test]
        public void TheSameCrashIsRecordedOncePerSession()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.True);
                Assert.That(recorder.RecordCrash("E", "other text", "at A()"), Is.False);
                Assert.That(recorder.RecordCrash("E", "m", "at B()"), Is.True);

                Assert.That(_sinks[0].Lines, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public void OnlyALimitedNumberOfDifferentCrashesAreRecorded()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                for (var i = 0; i < TelemetryRecorder.MaxCrashSignatures + 5; i++)
                {
                    recorder.RecordCrash("E", "m", "at Frame" + i);
                }

                Assert.That(_sinks[0].Lines, Has.Count.EqualTo(64));
                Assert.That(
                    _log.FindAll(line => line.EndsWith("further new crashes are not recorded", StringComparison.Ordinal)),
                    Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void ACrashThatCouldNotBeWrittenCanBeRecordedAgain()
        {
            using (var recorder = Recorder(TelemetrySpec.Default, consent: true))
            {
                recorder.RecordMatchSummary(Summary());
                _sinks[0].Refuse = true;
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.False);

                _sinks[0].Refuse = false;
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.True);
            }
        }

        [Test]
        public void CrashReportsCanBeTurnedOffWhileSummariesStay()
        {
            var spec = new TelemetrySpec(crashReports: false);
            using (var recorder = Recorder(spec, consent: true))
            {
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.False);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.True);
                Assert.That(_sinks[0].Lines[0], Does.StartWith("{\"type\":\"matchSummary\""));
            }
        }

        [Test]
        public void TheNoneSinkRecordsNothingEvenWithConsent()
        {
            var spec = new TelemetrySpec(sink: TelemetrySink.None);
            using (var recorder = Recorder(spec, consent: true))
            {
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.False);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.False);
                Assert.That(_sinks, Is.Empty);
                Assert.That(_log, Does.Contain("[Telemetry] not recording: the sink is None"));
            }
        }

        [Test]
        public void ASessionOutsideTheSampleSkipsSummariesButKeepsCrashes()
        {
            _roll = 0.5d;
            var spec = new TelemetrySpec(sampleRate: 0.25f);
            using (var recorder = Recorder(spec, consent: true))
            {
                Assert.That(recorder.SampledIn, Is.False);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.False);
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.True);
                Assert.That(_log, Does.Contain("[Telemetry] this session is not sampled (sampleRate=0.25); match summaries are skipped"));
            }
        }

        [Test]
        public void ASessionInsideTheSampleRecordsSummaries()
        {
            _roll = 0.2d;
            var spec = new TelemetrySpec(sampleRate: 0.25f);
            using (var recorder = Recorder(spec, consent: true))
            {
                Assert.That(recorder.SampledIn, Is.True);
                Assert.That(recorder.RecordMatchSummary(Summary()), Is.True);
            }
        }

        [Test]
        public void ARateOfZeroNeverSamples()
        {
            _roll = 0d;
            using (var recorder = Recorder(new TelemetrySpec(sampleRate: 0f), consent: true))
            {
                Assert.That(recorder.SampledIn, Is.False);
            }
        }

        [Test]
        public void TickFlushesWithoutWaitingOnceTheIntervalPasses()
        {
            var spec = new TelemetrySpec(flushIntervalSeconds: 2f);
            using (var recorder = Recorder(spec, consent: true))
            {
                recorder.RecordMatchSummary(Summary());

                recorder.Tick(1.5f);
                Assert.That(_sinks[0].Flushes, Is.Empty);

                recorder.Tick(0.5f);
                Assert.That(_sinks[0].Flushes, Is.EqualTo(_flushedWithoutWaiting));

                recorder.Tick(float.NaN);
                recorder.Tick(-1f);
                recorder.Tick(1.9f);
                Assert.That(_sinks[0].Flushes, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void DisposingSendsWhatWasWrittenThenCloses()
        {
            var recorder = Recorder(TelemetrySpec.Default, consent: true);
            recorder.RecordMatchSummary(Summary());

            recorder.Dispose();

            Assert.That(_sinks[0].Flushes, Is.EqualTo(_flushedAndWaited));
            Assert.That(_sinks[0].Disposed, Is.True);
            Assert.That(recorder.IsRecording, Is.False);
            Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.False);
        }

        [Test]
        public void ASinkThatCannotBeMadeIsTriedOnceUntilConsentIsGivenAgain()
        {
            var attempts = 0;
            var recorder = new TelemetryRecorder(
                TelemetrySpec.Default,
                _origin,
                true,
                () =>
                {
                    attempts++;
                    throw new UnauthorizedAccessException("read-only folder");
                },
                () => _time,
                () => _roll,
                _log.Add);
            using (recorder)
            {
                Assert.That(recorder.RecordCrash("E", "m", "at A()"), Is.False);
                Assert.That(recorder.RecordCrash("E", "m", "at B()"), Is.False);
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(_log, Does.Contain("[Telemetry] cannot create the LocalFile sink (read-only folder); the record is lost"));

                recorder.SetConsent(false);
                recorder.SetConsent(true);
                recorder.RecordCrash("E", "m", "at C()");
                Assert.That(attempts, Is.EqualTo(2));
            }
        }

        [Test]
        public void TheLocalFileSinkLeavesOneLineForACrashAndNothingWithoutConsent()
        {
            var folder = Path.Combine(Path.GetTempPath(), "shooter-telemetry-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var spec = new TelemetrySpec(fileName: "crash.jsonl");
                var path = Path.Combine(folder, "crash.jsonl");
                using (var withoutConsent = FileRecorder(spec, folder, false))
                {
                    withoutConsent.RecordCrash(new InvalidOperationException("boom"));
                }

                Assert.That(File.Exists(path), Is.False);

                using (var withConsent = FileRecorder(spec, folder, true))
                {
                    withConsent.RecordCrash(new InvalidOperationException("boom"));
                    Assert.That(ReadLines(path), Has.Length.EqualTo(1), "written before the recorder closes");
                }

                var lines = ReadLines(path);
                Assert.That(lines, Has.Length.EqualTo(1));
                Assert.That(lines[0], Does.Contain("\"exception\":\"System.InvalidOperationException\""));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        // 書き手が開いたままでも読めるよう、書き込みの共有を許して読む (Windows では File.ReadAllLines が開けない)
        internal static string[] ReadLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                return text.Length == 0 ? Array.Empty<string>() : text.TrimEnd('\n').Split('\n');
            }
        }

        private TelemetryRecorder FileRecorder(TelemetrySpec spec, string folder, bool consent)
        {
            return new TelemetryRecorder(spec, _origin, consent, () => TelemetrySinks.Create(spec, folder, _log.Add), () => _time, () => _roll, _log.Add);
        }

        private TelemetryRecorder Recorder(TelemetrySpec spec, bool consent)
        {
            return new TelemetryRecorder(
                spec,
                _origin,
                consent,
                () =>
                {
                    var sink = new RecordingSink();
                    _sinks.Add(sink);
                    return sink;
                },
                () => _time,
                () => _roll,
                _log.Add);
        }

        private static TelemetryMatchSummary Summary()
        {
            return new TelemetryMatchSummary("m-1", "Domination", "Yard", 90f, null, Side.Alpha, Side.Alpha);
        }

        private sealed class RecordingSink : ITelemetrySink
        {
            public List<string> Lines { get; } = new List<string>();

            public List<bool> Flushes { get; } = new List<bool>();

            // 同意を外したときの Dispose は別スレッドで走るので、テストのスレッドから見えるよう volatile にする
            private volatile bool _disposed;

            public bool Disposed => _disposed;

            public bool Refuse { get; set; }

            public string Target => "memory";

            public bool Write(string line)
            {
                if (Refuse)
                {
                    return false;
                }

                Lines.Add(line);
                return true;
            }

            public void Flush(bool wait)
            {
                Flushes.Add(wait);
            }

            public void Dispose()
            {
                _disposed = true;
            }
        }
    }
}
