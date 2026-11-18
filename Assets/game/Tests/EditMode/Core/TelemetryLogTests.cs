using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TelemetryLogTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 30, 15, 250, DateTimeKind.Utc);
        private static readonly TelemetryOrigin _origin = new TelemetryOrigin("client", "abc123", "1.4.0", "WindowsPlayer");

        [Test]
        public void ACrashLineHoldsTheOriginAndTheException()
        {
            var line = TelemetryLog.Crash(_time, _origin, "InvalidOperationException", "boom", "at Foo.Bar()\nat Foo.Baz()");

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"crash\",\"time\":\"2026-11-18T09:30:15.250Z\",\"source\":\"client\",\"session\":\"abc123\","
                + "\"version\":\"1.4.0\",\"platform\":\"WindowsPlayer\",\"exception\":\"InvalidOperationException\","
                + "\"message\":\"boom\",\"stackTrace\":\"at Foo.Bar()\\nat Foo.Baz()\"}"));
            Assert.That(line, Does.Not.Contain("\n"), "one record is one line");
        }

        [Test]
        public void AnUnknownTypeAndEmptyTextsStillMakeALine()
        {
            var origin = new TelemetryOrigin("server", "s1", null, " ");

            var values = Parse(TelemetryLog.Crash(_time, origin, " ", null, null));

            Assert.That(values["exception"], Is.EqualTo("unknown"));
            Assert.That(values["message"], Is.EqualTo(string.Empty));
            Assert.That(values["stackTrace"], Is.EqualTo(string.Empty));
            Assert.That(values.ContainsKey("version"), Is.False);
            Assert.That(values.ContainsKey("platform"), Is.False);
        }

        [Test]
        public void LongTextsAreCutSoOneLineStaysUnderTheSmallestFileLimit()
        {
            // 改行は 2 文字、制御文字は 6 文字に書き出されるので、いちばん膨らむ文字で埋める
            var stack = new string('\u0001', 20000);
            var message = new string('\u0002', 5000);
            var type = new string('T', 1000);

            var line = TelemetryLog.Crash(_time, _origin, type, message, stack);
            var values = Parse(line);

            Assert.That(values["stackTrace"].Length, Is.EqualTo(8192));
            Assert.That(values["stackTrace"], Does.EndWith("..."));
            Assert.That(values["message"].Length, Is.EqualTo(1024));
            Assert.That(values["exception"].Length, Is.EqualTo(256));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(line) + 1, Is.LessThan(TelemetrySpec.MinMaxFileBytes));
        }

        [Test]
        public void TheCutDoesNotSplitASurrogatePair()
        {
            var stack = new string('a', 8188) + "\U0001F600" + new string('b', 100);

            var values = Parse(TelemetryLog.Crash(_time, _origin, "E", "m", stack));

            Assert.That(values["stackTrace"], Is.EqualTo(new string('a', 8188) + "..."));
        }

        [Test]
        public void AMatchSummaryHoldsTheResultForTheOwnSide()
        {
            var summary = new TelemetryMatchSummary(
                "m-1",
                "CaptureTheFlag",
                "Harbor",
                312.5f,
                new[] { new SideScore(Side.Alpha, 3), new SideScore(Side.Bravo, 1.25) },
                Side.Alpha,
                Side.Bravo,
                humans: 6,
                bots: 2);

            var line = TelemetryLog.MatchSummary(_time, _origin, summary);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"matchSummary\",\"time\":\"2026-11-18T09:30:15.250Z\",\"source\":\"client\",\"session\":\"abc123\","
                + "\"version\":\"1.4.0\",\"platform\":\"WindowsPlayer\",\"matchId\":\"m-1\",\"mode\":\"CaptureTheFlag\",\"map\":\"Harbor\","
                + "\"durationSeconds\":\"312.5\",\"winner\":\"0\",\"ownSide\":\"1\",\"result\":\"lost\",\"score0\":\"3\",\"score1\":\"1.25\","
                + "\"humans\":\"6\",\"bots\":\"2\"}"));
        }

        [Test]
        public void ASummaryWithoutAWinnerOrOwnSideLeavesThoseOut()
        {
            var summary = new TelemetryMatchSummary(null, null, null, 0f, null, null);

            var values = Parse(TelemetryLog.MatchSummary(_time, _origin, summary));

            Assert.That(values["winner"], Is.EqualTo("none"));
            Assert.That(values["durationSeconds"], Is.EqualTo("0"));
            Assert.That(values.ContainsKey("matchId"), Is.False);
            Assert.That(values.ContainsKey("ownSide"), Is.False);
            Assert.That(values.ContainsKey("result"), Is.False);
            Assert.That(values.ContainsKey("humans"), Is.False);
        }

        [Test]
        public void ARepeatedSideKeepsTheFirstScore()
        {
            var summary = new TelemetryMatchSummary(
                "m", "Mode", "Map", 1f, new[] { new SideScore(Side.Alpha, 4), new SideScore(Side.Alpha, 9) }, null);

            var values = Parse(TelemetryLog.MatchSummary(_time, _origin, summary));

            Assert.That(values["score0"], Is.EqualTo("4"));
        }

        [Test]
        public void TheResultComparesTheWinnerWithTheOwnSide()
        {
            Assert.That(TelemetryLog.ResultOf(Side.Alpha, Side.Alpha), Is.EqualTo("won"));
            Assert.That(TelemetryLog.ResultOf(Side.Alpha, Side.Bravo), Is.EqualTo("lost"));
            Assert.That(TelemetryLog.ResultOf(Side.Alpha, null), Is.EqualTo("draw"));
        }

        [Test]
        public void ABadSummaryIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryMatchSummary("m", "x", "y", -1f, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryMatchSummary("m", "x", "y", float.NaN, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryMatchSummary("m", "x", "y", 1f, null, null, humans: -1));
            Assert.Throws<ArgumentException>(() => new TelemetryOrigin(" ", "s", "v", "p"));
            Assert.Throws<ArgumentException>(() => new TelemetryOrigin("client", "", "v", "p"));
        }

        [Test]
        public void ALocalTimeIsRefused()
        {
            var local = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Local);

            Assert.Throws<ArgumentException>(() => TelemetryLog.Crash(local, _origin, "E", "m", "s"));
        }

        [Test]
        public void ANewSessionIsThirtyTwoHexDigits()
        {
            var first = TelemetryOrigin.NewSession();
            var second = TelemetryOrigin.NewSession();

            Assert.That(first, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        private static IReadOnlyDictionary<string, string> Parse(string line)
        {
            Assert.That(FlatJsonObject.TryParse(line, out var values, out var error), Is.True, error);
            return values;
        }
    }
}
