using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TelemetrySinkTests
    {
        private static readonly Uri _endpoint = new Uri("https://telemetry.example.com/ingest");
        private static readonly string[] _twoLines = { "{\"n\":\"1\"}", "{\"n\":\"2\"}" };
        private static readonly string[] _lastTwo = { "{\"n\":\"2\"}", "{\"n\":\"3\"}" };
        private static readonly string[] _thirdOnly = { "{\"n\":\"3\"}" };
        private static readonly string[] _twoLinesBody = { "{\"n\":\"1\"}\n{\"n\":\"2\"}\n" };
        private static readonly string[] _jsonLinesType = { "application/x-ndjson" };

        private string _folder;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "shooter-telemetry-tests-" + Guid.NewGuid().ToString("N"));
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void TheFileSinkAppendsOneLineAtATimeEndedByANewline()
        {
            var path = Path.Combine(_folder, "nested", "telemetry.jsonl");
            using (var sink = new JsonLinesTelemetrySink(path, TelemetrySpec.MinMaxFileBytes, _log.Add))
            {
                Assert.That(sink.Write(_twoLines[0]), Is.True);
                Assert.That(sink.Write(_twoLines[1]), Is.True);
                Assert.That(sink.Target, Is.EqualTo(Path.GetFullPath(path)));
            }

            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"n\":\"1\"}\n{\"n\":\"2\"}\n"));
        }

        [Test]
        public void TheFileSinkAddsToAFileFromAnEarlierSession()
        {
            var path = Path.Combine(_folder, "telemetry.jsonl");
            using (var first = new JsonLinesTelemetrySink(path, TelemetrySpec.MinMaxFileBytes, _log.Add))
            {
                first.Write(_twoLines[0]);
            }

            using (var second = new JsonLinesTelemetrySink(path, TelemetrySpec.MinMaxFileBytes, _log.Add))
            {
                second.Write(_twoLines[1]);
            }

            Assert.That(TelemetryRecorderTests.ReadLines(path), Is.EqualTo(_twoLines));
        }

        [Test]
        public void AFullFileMovesToThePreviousOneAndStartsAgain()
        {
            var path = Path.Combine(_folder, "telemetry.jsonl");
            // 1 行は 10 バイト (改行込み) なので、上限 25 バイトには 2 行まで入る
            using (var sink = new JsonLinesTelemetrySink(path, 25, _log.Add))
            {
                sink.Write("{\"n\":\"1\"}");
                sink.Write("{\"n\":\"2\"}");
                sink.Write("{\"n\":\"3\"}");
            }

            Assert.That(TelemetryRecorderTests.ReadLines(path + ".1"), Is.EqualTo(_twoLines));
            Assert.That(TelemetryRecorderTests.ReadLines(path), Is.EqualTo(_thirdOnly));
            Assert.That(_log, Has.Count.EqualTo(1));
            Assert.That(_log[0], Does.StartWith("[Telemetry] " + Path.GetFullPath(path) + " reached its size limit; moved it to "));
        }

        [Test]
        public void ALineLongerThanTheLimitIsStillWrittenAlone()
        {
            var path = Path.Combine(_folder, "telemetry.jsonl");
            var longLine = "{\"n\":\"" + new string('x', 40) + "\"}";
            using (var sink = new JsonLinesTelemetrySink(path, 25, _log.Add))
            {
                Assert.That(sink.Write(longLine), Is.True);
            }

            Assert.That(TelemetryRecorderTests.ReadLines(path), Is.EqualTo(new[] { longLine }));
            Assert.That(File.Exists(path + ".1"), Is.False, "an empty file is not moved aside");
        }

        [Test]
        public void TheFileSinkReportsAFailureInsteadOfThrowing()
        {
            // 置き場所にフォルダと同じ名前のファイルがあると、フォルダを作れない
            Directory.CreateDirectory(_folder);
            var blocker = Path.Combine(_folder, "blocked");
            File.WriteAllText(blocker, "not a folder");
            using (var sink = new JsonLinesTelemetrySink(Path.Combine(blocker, "telemetry.jsonl"), TelemetrySpec.MinMaxFileBytes, _log.Add))
            {
                Assert.That(sink.Write(_twoLines[0]), Is.False);
            }

            Assert.That(_log, Has.Count.EqualTo(1));
            Assert.That(_log[0], Does.StartWith("[Telemetry] could not write to "));
            Assert.That(_log[0], Does.EndWith("; the record is lost"));
        }

        [Test]
        public void AClosedFileSinkWritesNothing()
        {
            var path = Path.Combine(_folder, "telemetry.jsonl");
            var sink = new JsonLinesTelemetrySink(path, TelemetrySpec.MinMaxFileBytes, _log.Add);
            sink.Dispose();

            Assert.That(sink.Write(_twoLines[0]), Is.False);
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void TheFactoryPicksTheSinkFromTheSpec()
        {
            Assert.That(TelemetrySinks.Create(new TelemetrySpec(sink: TelemetrySink.None), _folder, _log.Add), Is.Null);

            using (var file = TelemetrySinks.Create(new TelemetrySpec(fileName: "events.jsonl"), _folder, _log.Add))
            {
                Assert.That(file, Is.InstanceOf<JsonLinesTelemetrySink>());
                Assert.That(file.Target, Is.EqualTo(Path.GetFullPath(Path.Combine(_folder, "events.jsonl"))));
            }

            var http = new TelemetrySpec(sink: TelemetrySink.Http, endpoint: _endpoint.ToString());
            using (var handler = new RecordingHandler())
            using (var sink = TelemetrySinks.Create(http, _folder, _log.Add, handler))
            {
                Assert.That(sink, Is.InstanceOf<HttpTelemetrySink>());
                Assert.That(((HttpTelemetrySink)sink).MaxPendingBytes, Is.EqualTo(TelemetrySpec.DefaultMaxFileBytes));
            }

            Assert.Throws<ArgumentException>(() => TelemetrySinks.Create(TelemetrySpec.Default, " ", _log.Add));
        }

        [Test]
        public void TheHttpSinkSendsTheLinesAsOneJsonLinesBody()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler))
            {
                sink.Write(_twoLines[0]);
                sink.Write(_twoLines[1]);
                Assert.That(handler.Bodies, Is.Empty, "nothing is sent before a flush");

                sink.Flush(true);

                Assert.That(handler.Bodies, Is.EqualTo(_twoLinesBody));
                Assert.That(handler.ContentTypes, Is.EqualTo(_jsonLinesType));
                Assert.That(handler.Uris, Is.EqualTo(new[] { _endpoint }));
                Assert.That(sink.Pending, Is.Zero);
                Assert.That(_log, Is.Empty);
            }
        }

        [Test]
        public void AFlushWithNothingWrittenSendsNothing()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler))
            {
                sink.Flush(true);

                Assert.That(handler.Bodies, Is.Empty);
            }
        }

        [Test]
        public void AServerErrorKeepsTheLinesForTheNextFlush()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler))
            {
                handler.Statuses.Enqueue(HttpStatusCode.ServiceUnavailable);
                sink.Write(_twoLines[0]);
                sink.Flush(true);

                Assert.That(sink.Pending, Is.EqualTo(1));
                Assert.That(_log, Does.Contain(
                    "[Telemetry] could not send 1 record(s) to https://telemetry.example.com/ingest (HTTP 503); keeping them for the next send"));

                sink.Write(_twoLines[1]);
                sink.Flush(true);

                Assert.That(handler.Bodies[1], Is.EqualTo("{\"n\":\"1\"}\n{\"n\":\"2\"}\n"), "the kept lines go first");
                Assert.That(sink.Pending, Is.Zero);
            }
        }

        [Test]
        public void ARefusedBodyIsDropped()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler))
            {
                handler.Statuses.Enqueue(HttpStatusCode.BadRequest);
                sink.Write(_twoLines[0]);
                sink.Flush(true);

                Assert.That(sink.Pending, Is.Zero);
                Assert.That(_log, Does.Contain(
                    "[Telemetry] could not send 1 record(s) to https://telemetry.example.com/ingest (HTTP 400); dropping them"));
            }
        }

        [Test]
        public void ANetworkFailureKeepsTheLines()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler))
            {
                handler.Failure = new HttpRequestException("no route to host");
                sink.Write(_twoLines[0]);
                sink.Flush(true);

                Assert.That(sink.Pending, Is.EqualTo(1));
                Assert.That(_log[0], Does.Contain("(HttpRequestException: no route to host); keeping them for the next send"));
            }
        }

        [Test]
        public void TheOldestLinesAreDroppedToStayUnderTheLimit()
        {
            using (var handler = new RecordingHandler())
            using (var sink = new HttpTelemetrySink(_endpoint, 25, _log.Add, handler))
            {
                sink.Write("{\"n\":\"1\"}");
                sink.Write("{\"n\":\"2\"}");
                sink.Write("{\"n\":\"3\"}");

                Assert.That(sink.Pending, Is.EqualTo(2));
                Assert.That(_log, Does.Contain("[Telemetry] dropped 1 unsent record(s) for https://telemetry.example.com/ingest to stay under the size limit"));

                sink.Flush(true);
                Assert.That(handler.Bodies[0], Is.EqualTo(string.Join("\n", _lastTwo) + "\n"));
            }
        }

        [Test]
        public void DisposingDropsWhatWasNotSent()
        {
            using (var handler = new RecordingHandler())
            {
                var sink = new HttpTelemetrySink(_endpoint, TelemetrySpec.MinMaxFileBytes, _log.Add, handler);
                sink.Write(_twoLines[0]);

                sink.Dispose();
                sink.Flush(true);

                Assert.That(handler.Bodies, Is.Empty);
                Assert.That(sink.Write(_twoLines[1]), Is.False);
            }
        }

        [Test]
        public void OnlyHttpsEndpointsAreAccepted()
        {
            Assert.Throws<ArgumentException>(() => new HttpTelemetrySink(new Uri("http://telemetry.example.com"), 1024, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HttpTelemetrySink(_endpoint, 0, _log.Add));
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public Queue<HttpStatusCode> Statuses { get; } = new Queue<HttpStatusCode>();

            public List<string> Bodies { get; } = new List<string>();

            public List<string> ContentTypes { get; } = new List<string>();

            public List<Uri> Uris { get; } = new List<Uri>();

            public Exception Failure { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Failure != null)
                {
                    var failure = Failure;
                    Failure = null;
                    throw failure;
                }

                lock (Bodies)
                {
                    // Unity の .NET Standard 2.1 には CancellationToken を取る ReadAsStringAsync がない。
#pragma warning disable CA2016
                    Bodies.Add(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
#pragma warning restore CA2016
                    ContentTypes.Add(request.Content.Headers.ContentType.MediaType);
                    Uris.Add(request.RequestUri);
                }

                var status = Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(status));
            }
        }
    }
}
