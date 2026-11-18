using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using ReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.Server.Tests
{
    public sealed class ServerReportsTests
    {
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        private TempDirectory _folder;
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerReports _reports;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _folder = new TempDirectory();
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _match = new ServerMatch(0f, _log, new ServerEventWriter(false, new StringWriter(), _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(_transport),
                new StubControl());
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _reports?.Dispose();
            _reports = null;
            _transport.Dispose();
            _log.Dispose();
            _folder.Dispose();
        }

        private string FilePath => Path.Combine(_folder.Path, "reports", ServerReports.DefaultFileName);

        [Test]
        public void OneReportIsSavedAsOneLine()
        {
            Start();
            _match.Join(new ConnectionId(1), "device1", _alice);
            _match.Join(new ConnectionId(2), "device2", _bob);

            Report(1, 2, ReportReason.AbusiveChat, "  暴言を\n繰り返す \"x\" ");

            Assert.That(Lines(), Is.EqualTo(new[]
            {
                "{\"type\":\"report\",\"time\":\"2026-11-15T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\","
                    + "\"reporterId\":\"1\",\"reporterProductUserId\":\"" + _alice + "\","
                    + "\"targetId\":\"2\",\"targetProductUserId\":\"" + _bob + "\","
                    + "\"reason\":\"AbusiveChat\",\"comment\":\"暴言を\\n繰り返す \\\"x\\\"\"}",
            }));
            Assert.That(File.ReadAllText(FilePath), Does.EndWith("}\n"));
            Assert.That(_log.ToString(), Does.Contain("[Report] saved reporter=1 target=2 reason=AbusiveChat"));
            Assert.That(_log.ToString(), Does.Not.Contain("暴言"), "the comment stays out of the server log");
        }

        [Test]
        public void AReportWithoutProductUserIdsOrCommentLeavesThoseOut()
        {
            Start();
            Join(1);
            Join(2);

            Report(2, 1, ReportReason.Cheating, "   ");

            Assert.That(Lines(), Is.EqualTo(new[]
            {
                "{\"type\":\"report\",\"time\":\"2026-11-15T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\","
                    + "\"reporterId\":\"2\",\"targetId\":\"1\",\"reason\":\"Cheating\"}",
            }));
        }

        [Test]
        public void TheSameReportSentAgainIsNotSaved()
        {
            Start();
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Cheating);
            Report(1, 2, ReportReason.Cheating);
            Report(1, 2, ReportReason.Griefing, "another reason");

            Assert.That(Lines().Length, Is.EqualTo(1));
            Assert.That(Lines()[0], Does.Contain("\"reason\":\"Cheating\""));
            Assert.That(_log.ToString(), Does.Contain("[Report] skipped a repeated report reporter=1 target=2"));
        }

        [Test]
        public void TheSameReportIsSavedAgainOnceTheRepeatIntervalHasPassed()
        {
            Start(10d);
            Join(1);
            Join(2);
            Report(1, 2, ReportReason.Cheating);

            Advance(9.5f);
            Report(1, 2, ReportReason.Cheating);
            Assert.That(Lines().Length, Is.EqualTo(1));

            Advance(0.5f);
            Report(1, 2, ReportReason.Cheating);
            Assert.That(Lines().Length, Is.EqualTo(2));
        }

        [Test]
        public void AZeroRepeatIntervalSavesEveryReport()
        {
            Start(0d);
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Cheating);
            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines().Length, Is.EqualTo(2));
        }

        [Test]
        public void OtherTargetsAndOtherReportersAreNotHeldBack()
        {
            Start();
            Join(1);
            Join(2);
            Join(3);

            Report(1, 2, ReportReason.Cheating);
            Report(1, 3, ReportReason.Cheating);
            Report(2, 1, ReportReason.Cheating);
            Report(3, 2, ReportReason.Cheating);

            var lines = Lines();
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[1], Does.Contain("\"reporterId\":\"1\",\"targetId\":\"3\""));
            Assert.That(lines[3], Does.Contain("\"reporterId\":\"3\",\"targetId\":\"2\""));
        }

        [Test]
        public void ReportsOnOneselfOnBotsAndOnPlayersNotInTheMatchAreRefused()
        {
            Start();
            Join(1);
            Join(2);
            _match.JoinBot(BotRoster.FirstBotPlayerId);

            Report(1, 1, ReportReason.Cheating);
            Report(1, BotRoster.FirstBotPlayerId, ReportReason.Cheating);
            Report(1, 9, ReportReason.Cheating);

            Assert.That(File.Exists(FilePath), Is.True);
            Assert.That(Lines(), Is.Empty);
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Report] refused reporter=1 target=1 reason=Self"));
            Assert.That(log, Does.Contain("[Report] refused reporter=1 target=" + BotRoster.FirstBotPlayerId + " reason=Bot"));
            Assert.That(log, Does.Contain("[Report] refused reporter=1 target=9 reason=UnknownTarget"));
        }

        [Test]
        public void AReportFromAConnectionThatHasNotJoinedIsIgnored()
        {
            Start();
            Join(2);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Report] ignored a report from connection 1 that has not joined"));
        }

        [Test]
        public void OtherMessagesAreLeftAlone()
        {
            Start();
            Join(1);

            _reports.OnMessage(new ConnectionId(1), new ChatRequestMessage { Text = "hi" });

            Assert.That(Lines(), Is.Empty);
        }

        [Test]
        public void LeavingForgetsTheReportersRepeats()
        {
            Start();
            Join(1);
            Join(2);
            Report(1, 2, ReportReason.Cheating);

            _match.Leave(new ConnectionId(1));
            _reports.OnDisconnected(new ConnectionId(1));
            Join(1);
            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines().Length, Is.EqualTo(2));
        }

        [Test]
        public void ReportsAreAddedAfterWhatTheFileAlreadyHolds()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, "{\"type\":\"report\"}\n");
            Start();
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Other);
            _reports.Stop();

            var lines = Lines();
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo("{\"type\":\"report\"}"));
            Assert.That(lines[1], Does.Contain("\"reason\":\"Other\""));
            Assert.That(_log.ToString(), Does.Contain("[Report] closed " + FilePath));
        }

        [Test]
        public void StartStopsTheServerWhenTheFileCannotBeOpened()
        {
            Directory.CreateDirectory(FilePath);
            _reports = new ServerReports(FilePath, () => _time);

            var error = Assert.Throws<InvalidOperationException>(() => _reports.Start(_context));
            Assert.That(error.Message, Does.StartWith("[Report] cannot open the report file " + FilePath));
        }

        [Test]
        public void AReportAfterStopIsLostAndLogged()
        {
            Start();
            Join(1);
            Join(2);
            _reports.Stop();

            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Report] could not write a report to " + FilePath + " (the report file is closed)"));
        }

        [Test]
        public void TheConstructorRejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => new ServerReports(" ", () => _time));
            Assert.Throws<ArgumentNullException>(() => new ServerReports(FilePath, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerReports(FilePath, () => _time, -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerReports(FilePath, () => _time, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerReports(FilePath, () => _time, double.PositiveInfinity));
        }

        [Test]
        public void TheDefaultsAreFiveMinutesAndReportsJsonl()
        {
            using var reports = new ServerReports(FilePath, () => _time);

            Assert.That(reports.RepeatSeconds, Is.EqualTo(300d));
            Assert.That(reports.Name, Is.EqualTo("reports"));
            Assert.That(ServerReports.DefaultFileName, Is.EqualTo("reports.jsonl"));
        }

        [Test]
        public void TheRecordRefusesAMissingMatchIdOrAnUnknownReason()
        {
            Assert.Throws<ArgumentException>(() => ServerReportLog.Record(_time, "", 1, null, 2, null, ReportReason.Cheating, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerReportLog.Record(_time, "m", 1, null, 2, null, (ReportReason)99, null));
            Assert.Throws<ArgumentException>(
                () => ServerReportLog.Record(DateTime.SpecifyKind(_time, DateTimeKind.Local), "m", 1, null, 2, null, ReportReason.Cheating, null));
        }

        private void Start(double repeatSeconds = ServerReports.DefaultRepeatSeconds)
        {
            _reports = new ServerReports(FilePath, () => _time, repeatSeconds);
            _reports.Start(_context);
            Assert.That(_log.ToString(), Does.Contain("[Report] writing reports to " + FilePath));
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private void Report(int from, int target, ReportReason reason, string comment = "")
        {
            _reports.OnMessage(
                new ConnectionId((ulong)from), new ReportRequestMessage { TargetPlayerId = target, Reason = reason, Comment = comment });
        }

        private void Advance(float seconds)
        {
            _reports.Tick(++_tick, seconds);
        }

        private string[] Lines()
        {
            if (!File.Exists(FilePath))
            {
                return Array.Empty<string>();
            }

            // 書き手が開いたままでも読めるよう、共有を許して開く
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text.Length == 0 ? Array.Empty<string>() : text.TrimEnd('\n').Split('\n');
        }
    }
}
