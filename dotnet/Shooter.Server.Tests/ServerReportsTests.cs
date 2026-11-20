using System;
using System.IO;
using System.Linq;
using System.Numerics;
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
        private static readonly string[] _firstEvidenceFile = { "report-1.evidence.jsonl" };
        private static readonly string[] _limitedEvidenceFiles = { "report-1.evidence.jsonl", "report-2.evidence.jsonl" };
        private static readonly string[] _otherReporterEvidenceFiles = { "report-1.evidence.jsonl", "report-3.evidence.jsonl" };
        private static readonly string[] _afterAMinuteEvidenceFiles =
        {
            "report-1.evidence.jsonl", "report-2.evidence.jsonl", "report-4.evidence.jsonl",
        };

        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        private TempDirectory _folder;
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerReports _reports;
        private ServerReportEvidence _evidence;
        private long _tick;
        private int _ids;

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
                new StubControl(),
                addresses: new PerConnectionAddresses());
            _tick = 0;
            _ids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _reports?.Dispose();
            _reports = null;
            _evidence?.Stop();
            _evidence = null;
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
                "{\"type\":\"report\",\"id\":\"report-1\",\"time\":\"2026-11-15T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\","
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
                "{\"type\":\"report\",\"id\":\"report-1\",\"time\":\"2026-11-15T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\","
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
        public void AReportWritesTheTargetsEvidenceUnderTheSameIdNextToTheReports()
        {
            var signals = new ServerCheatSignals();
            var evidence = StartEvidence(signals, true);
            Start(evidence: evidence);
            Join(1);
            Join(2);
            _context.Bodies.Add(2).SetPose(new Vector3(4f, 0f, 6f), 90f, PlayerStance.Standing);
            _context.Bodies.Add(1).SetPose(new Vector3(-1f, 0f, -1f), 0f, PlayerStance.Standing);
            signals.RaiseLooked(1, 7, 0f, null);
            signals.RaiseLooked(2, 7, 90f, null);
            Advance(0.5f);
            signals.RaiseShotAccepted(2, new Vector3(4f, 1.5f, 6f), new Vector3(0f, 0f, 1f), 37f);
            signals.RaiseHit(2, 1, HitZone.Head, 3f);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(1));
            Assert.That(Lines()[0], Does.StartWith("{\"type\":\"report\",\"id\":\"report-1\","));
            var path = Path.Combine(_folder.Path, "reports", "report-1.evidence.jsonl");
            Assert.That(_reports.EvidencePath("report-1"), Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(File.ReadAllText(path), Is.EqualTo(
                "{\"type\":\"look\",\"ago\":\"0.500\",\"tick\":\"7\",\"x\":\"4\",\"y\":\"0\",\"z\":\"6\",\"yaw\":\"90\"}\n"
                    + "{\"type\":\"shot\",\"ago\":\"0.000\",\"tick\":\"37\",\"x\":\"4\",\"y\":\"1.5\",\"z\":\"6\",\"dirX\":\"0\",\"dirY\":\"0\",\"dirZ\":\"1\"}\n"
                    + "{\"type\":\"hit\",\"ago\":\"0.000\",\"victimId\":\"1\",\"zone\":\"Head\",\"rewindTicks\":\"3\"}\n"));
            Assert.That(_log.ToString(), Does.Contain("[Evidence] saved report=report-1 target=2 to " + Path.GetFullPath(path)));
        }

        [Test]
        public void ARepeatedReportWritesNoEvidence()
        {
            var signals = new ServerCheatSignals();
            Start(evidence: StartEvidence(signals, true));
            Join(1);
            Join(2);
            signals.RaiseLooked(2, 1, 0f, null);

            Report(1, 2, ReportReason.Cheating);
            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(1));
            Assert.That(EvidenceFiles(), Is.EqualTo(_firstEvidenceFile));
            Assert.That(_ids, Is.EqualTo(1), "a held-back report takes no id");
        }

        [Test]
        public void EvidenceStopsAtTheFilesPerMinuteAndTheReportsAreStillKept()
        {
            var signals = new ServerCheatSignals();
            Start(0d, StartEvidence(signals, true, 2));
            for (var id = 1; id <= 4; id++)
            {
                Join(id);
            }

            Report(1, 2, ReportReason.Cheating);
            Report(2, 3, ReportReason.Cheating);
            Report(3, 4, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(3));
            Assert.That(EvidenceFiles(), Is.EqualTo(_limitedEvidenceFiles));
            Assert.That(_log.ToString(), Does.Contain(
                "[Evidence] skipped the evidence for target=4: already 2 files in the last minute (evidence.maxFilesPerMinute); the report is kept without it"));

            Advance(60f);
            Report(1, 4, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(4));
            Assert.That(EvidenceFiles(), Is.EqualTo(_afterAMinuteEvidenceFiles));
        }

        [Test]
        public void AReporterGetsOneEvidenceFilePerMinuteAndTheReportsAreStillKept()
        {
            var signals = new ServerCheatSignals();
            Start(0d, StartEvidence(signals, true));
            for (var id = 1; id <= 3; id++)
            {
                Join(id);
            }

            Report(1, 2, ReportReason.Cheating);
            Report(1, 3, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(2));
            Assert.That(EvidenceFiles(), Is.EqualTo(_firstEvidenceFile));
            Assert.That(_log.ToString(), Does.Contain(
                "[Evidence] skipped the evidence for target=3: the reporter already had evidence saved in the last minute; the report is kept without it"));

            Report(2, 3, ReportReason.Cheating);

            Assert.That(EvidenceFiles(), Is.EqualTo(_otherReporterEvidenceFiles), "another reporter still gets a file");
        }

        [Test]
        public void ARefusedReportWritesNoEvidence()
        {
            var signals = new ServerCheatSignals();
            Start(evidence: StartEvidence(signals, true));
            Join(1);
            signals.RaiseLooked(1, 1, 0f, null);

            Report(1, 1, ReportReason.Cheating);
            Report(1, 9, ReportReason.Cheating);

            Assert.That(EvidenceFiles(), Is.Empty);
        }

        [Test]
        public void DisabledEvidenceWritesNoFile()
        {
            var signals = new ServerCheatSignals();
            Start(evidence: StartEvidence(signals, false));
            Join(1);
            Join(2);
            signals.RaiseLooked(2, 1, 0f, null);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(1));
            Assert.That(EvidenceFiles(), Is.Empty);
            Assert.That(_log.ToString(), Does.Not.Contain("[Evidence] saved"));
        }

        [Test]
        public void ATargetWithoutRecordsGetsAnEmptyEvidenceFile()
        {
            Start(evidence: StartEvidence(new ServerCheatSignals(), true));
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(File.ReadAllText(Path.Combine(_folder.Path, "reports", "report-1.evidence.jsonl")), Is.Empty);
        }

        [Test]
        public void AnEvidenceFileThatAlreadyExistsIsKeptAndTheReportIsStillSaved()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var existing = Path.Combine(_folder.Path, "reports", "same.evidence.jsonl");
            File.WriteAllText(existing, "earlier\n");
            var evidence = new CountingEvidence("{\"type\":\"look\"}\n");
            _reports = new ServerReports(FilePath, () => _time, 0d, evidence, () => "same");
            _reports.Start(_context);
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(Lines(), Has.Length.EqualTo(1));
            Assert.That(evidence.Asked, Is.EqualTo(1));
            Assert.That(File.ReadAllText(existing), Is.EqualTo("earlier\n"));
            Assert.That(_log.ToString(), Does.Contain("[Evidence] could not write the evidence to " + Path.GetFullPath(existing) + " ("));
            Assert.That(_log.ToString(), Does.Contain("[Report] saved reporter=1 target=2 reason=Cheating"));
        }

        [Test]
        public void AnEvidenceThatDescribesNothingWritesNoFile()
        {
            var evidence = new CountingEvidence(null);
            Start(evidence: evidence);
            Join(1);
            Join(2);

            Report(1, 2, ReportReason.Cheating);

            Assert.That(_reports.Evidence, Is.SameAs(evidence));
            Assert.That(evidence.Asked, Is.EqualTo(1));
            Assert.That(evidence.LastTarget, Is.EqualTo(2));
            Assert.That(Lines(), Has.Length.EqualTo(1));
            Assert.That(EvidenceFiles(), Is.Empty);
        }

        [Test]
        public void TheEvidencePathRefusesTextThatIsNotAReportId()
        {
            using var reports = new ServerReports(FilePath, () => _time);

            Assert.Throws<ArgumentException>(() => reports.EvidencePath("../x"));
            Assert.Throws<ArgumentException>(() => reports.EvidencePath(""));
            Assert.That(ServerReportLog.IsReportId(ServerReportLog.NewReportId()), Is.True);
            Assert.That(ServerReportLog.IsReportId(new string('a', ServerReportLog.MaxReportIdLength + 1)), Is.False);
            Assert.That(ServerReportLog.IsReportId("a/b"), Is.False);
            Assert.That(ServerReportLog.IsReportId("a.b"), Is.False);
        }

        [Test]
        public void TheDefaultsAreFiveMinutesAndReportsJsonl()
        {
            using var reports = new ServerReports(FilePath, () => _time);

            Assert.That(reports.Evidence, Is.Null);
            Assert.That(reports.RepeatSeconds, Is.EqualTo(300d));
            Assert.That(reports.Name, Is.EqualTo("reports"));
            Assert.That(ServerReports.DefaultFileName, Is.EqualTo("reports.jsonl"));
        }

        [Test]
        public void TheRecordRefusesAMissingMatchIdOrAnUnknownReason()
        {
            Assert.Throws<ArgumentException>(() => ServerReportLog.Record("r", _time, "", 1, null, 2, null, ReportReason.Cheating, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerReportLog.Record("r", _time, "m", 1, null, 2, null, (ReportReason)99, null));
            Assert.Throws<ArgumentException>(
                () => ServerReportLog.Record("r", DateTime.SpecifyKind(_time, DateTimeKind.Local), "m", 1, null, 2, null, ReportReason.Cheating, null));
            Assert.Throws<ArgumentException>(() => ServerReportLog.Record("", _time, "m", 1, null, 2, null, ReportReason.Cheating, null));
            Assert.Throws<ArgumentException>(() => ServerReportLog.Record("..\\x", _time, "m", 1, null, 2, null, ReportReason.Cheating, null));
        }

        private void Start(double repeatSeconds = ServerReports.DefaultRepeatSeconds, IReportEvidence evidence = null)
        {
            _reports = new ServerReports(FilePath, () => _time, repeatSeconds, evidence, () => "report-" + ++_ids);
            _reports.Start(_context);
            Assert.That(_log.ToString(), Does.Contain("[Report] writing reports to " + FilePath));
        }

        private ServerReportEvidence StartEvidence(
            ServerCheatSignals signals, bool enabled, int maxFilesPerMinute = AntiCheatTuning.EvidenceSettings.DefaultMaxFilesPerMinute)
        {
            _evidence = new ServerReportEvidence(signals, new AntiCheatTuning.EvidenceSettings(enabled, 30f, maxFilesPerMinute));
            _evidence.Start(_context);
            return _evidence;
        }

        private string[] EvidenceFiles()
        {
            var folder = Path.GetDirectoryName(FilePath);
            return Directory.GetFiles(folder, "*" + ServerReportEvidenceLog.FileSuffix).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        // ProductUserId の無い通報者でも、接続ごとに違うアドレスの鍵で人ごとの枠が作れるようにする
        private sealed class PerConnectionAddresses : IConnectionAddresses
        {
            public bool TryGetRemoteAddress(ConnectionId connection, out string address)
            {
                address = "10.0.0." + connection.Value;
                return true;
            }
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
            ++_tick;
            _evidence?.Tick(_tick, seconds);
            _reports.Tick(_tick, seconds);
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

        private sealed class CountingEvidence : IReportEvidence
        {
            private readonly string _text;

            public CountingEvidence(string text)
            {
                _text = text;
            }

            public int Asked { get; private set; }

            public int LastTarget { get; private set; }

            public string Describe(int targetId, string reporterKey)
            {
                Asked++;
                LastTarget = targetId;
                return _text;
            }
        }
    }
}
