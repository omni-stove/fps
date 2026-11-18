using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TelemetryJsonTests
    {
        private const string _source = "telemetry.json";

        private static readonly string[] _expectedItemNames =
        {
            "sink", "fileName", "endpoint", "flushIntervalSeconds", "maxFileBytes", "crashReports", "sampleRate",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = TelemetryJson.Parse(
                "{\"sink\": \"Http\", \"fileName\": \"events.jsonl\", \"endpoint\": \"https://telemetry.example.com/ingest\","
                + " \"flushIntervalSeconds\": 2.5, \"maxFileBytes\": 1048576, \"crashReports\": false, \"sampleRate\": 0.25}",
                _source);

            Assert.That(spec.Sink, Is.EqualTo(TelemetrySink.Http));
            Assert.That(spec.FileName, Is.EqualTo("events.jsonl"));
            Assert.That(spec.Endpoint, Is.EqualTo("https://telemetry.example.com/ingest"));
            Assert.That(spec.FlushIntervalSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.MaxFileBytes, Is.EqualTo(1048576));
            Assert.That(spec.CrashReports, Is.False);
            Assert.That(spec.SampleRate, Is.EqualTo(0.25f));
            Assert.That(spec.IsOff, Is.False);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = TelemetryJson.Parse("{}", _source);

            Assert.That(spec.Sink, Is.EqualTo(TelemetrySink.LocalFile));
            Assert.That(spec.FileName, Is.EqualTo("telemetry.jsonl"));
            Assert.That(spec.Endpoint, Is.EqualTo(string.Empty));
            Assert.That(spec.FlushIntervalSeconds, Is.EqualTo(10f));
            Assert.That(spec.MaxFileBytes, Is.EqualTo(5242880));
            Assert.That(spec.CrashReports, Is.True);
            Assert.That(spec.SampleRate, Is.EqualTo(1f));
        }

        [Test]
        public void TheNoneSinkTurnsTelemetryOffAndIgnoresTheEndpoint()
        {
            var spec = TelemetryJson.Parse("{\"sink\": \"None\", \"endpoint\": \"not a url\"}", _source);

            Assert.That(spec.IsOff, Is.True);
            Assert.That(spec.Endpoint, Is.EqualTo("not a url"));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(TelemetryJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(TelemetryJson.ResourcePath, Is.EqualTo("Tuning/telemetry"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var noEndpoint = Reject("{\"sink\": \"Http\"}");
            var plainHttp = Reject("{\"sink\": \"Http\", \"endpoint\": \"http://telemetry.example.com\"}");
            var folder = Reject("{\"fileName\": \"logs/telemetry.jsonl\"}");
            var backslash = Reject("{\"fileName\": \"..\\\\telemetry.jsonl\"}");
            var parent = Reject("{\"fileName\": \"..\"}");
            var blank = Reject("{\"fileName\": \" \"}");
            var small = Reject("{\"maxFileBytes\": 1024}");
            var rate = Reject("{\"sampleRate\": 1.5}");
            var flush = Reject("{\"flushIntervalSeconds\": 0}");
            var sink = Reject("{\"sink\": \"Cloud\"}");

            Assert.That(noEndpoint.Item, Is.EqualTo("endpoint"));
            Assert.That(noEndpoint.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(plainHttp.Item, Is.EqualTo("endpoint"));
            Assert.That(plainHttp.Message, Does.Contain("https"));
            Assert.That(folder.Item, Is.EqualTo("fileName"));
            Assert.That(backslash.Item, Is.EqualTo("fileName"));
            Assert.That(parent.Item, Is.EqualTo("fileName"));
            Assert.That(blank.Item, Is.EqualTo("fileName"));
            Assert.That(small.Item, Is.EqualTo("maxFileBytes"));
            Assert.That(small.Message, Does.Contain("65536"));
            Assert.That(rate.Item, Is.EqualTo("sampleRate"));
            Assert.That(flush.Item, Is.EqualTo("flushIntervalSeconds"));
            Assert.That(sink.Item, Is.EqualTo("sink"));
            Assert.That(sink.Message, Does.Contain("None, LocalFile, Http"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => TelemetryJson.Parse(json, _source));
        }
    }
}
