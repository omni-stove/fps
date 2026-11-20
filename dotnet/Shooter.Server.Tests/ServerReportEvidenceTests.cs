using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerReportEvidenceTests
    {
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerCheatSignals _signals;
        private ServerReportEvidence _evidence;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _match = new ServerMatch(0f, _log, new ServerEventWriter(false, new StringWriter(), _log), () => DateTime.UtcNow);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(_transport),
                new StubControl());
            _signals = new ServerCheatSignals();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _evidence?.Stop();
            _evidence = null;
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void RecordsOlderThanTheSecondsAreDropped()
        {
            Start(true, 1f);
            _context.Bodies.Add(1).SetPose(new Vector3(1f, 0f, 2f), 90f, PlayerStance.Standing);

            _signals.RaiseLooked(1, 10, 90f, null);
            Advance(0.6f);
            _signals.RaiseLooked(1, 11, 95f, null);
            Advance(0.6f);

            Assert.That(_evidence.Describe(1), Is.EqualTo(
                "{\"type\":\"look\",\"ago\":\"0.600\",\"tick\":\"11\",\"x\":\"1\",\"y\":\"0\",\"z\":\"2\",\"yaw\":\"95\"}\n"));

            Advance(0.5f);

            Assert.That(_evidence.Describe(1), Is.Empty, "everything is older than the seconds now");
        }

        [Test]
        public void ARecordExactlyAsOldAsTheSecondsIsKept()
        {
            Start(true, 2f);

            _signals.RaiseLooked(1, 1, 0f, null);
            Advance(1f);
            Advance(1f);

            Assert.That(_evidence.Describe(1), Does.StartWith("{\"type\":\"look\",\"ago\":\"2.000\","));
        }

        [Test]
        public void ShotsAndHitsAreHeldForTheShooterNotTheVictim()
        {
            Start(true, 30f);

            _signals.RaiseShotAccepted(1, new Vector3(0f, 1.5f, 0f), new Vector3(0f, 0f, 1f), 12f);
            _signals.RaiseHit(1, 2, HitZone.Head, 2.5f);

            Assert.That(_evidence.Describe(1), Is.EqualTo(
                "{\"type\":\"shot\",\"ago\":\"0.000\",\"tick\":\"12\",\"x\":\"0\",\"y\":\"1.5\",\"z\":\"0\",\"dirX\":\"0\",\"dirY\":\"0\",\"dirZ\":\"1\"}\n"
                    + "{\"type\":\"hit\",\"ago\":\"0.000\",\"victimId\":\"2\",\"zone\":\"Head\",\"rewindTicks\":\"2.5\"}\n"));
            Assert.That(_evidence.Describe(2), Is.Empty);
        }

        [Test]
        public void ALookWithoutABodyLeavesThePositionOutAndAPitchIsKept()
        {
            Start(true, 30f);

            _signals.RaiseLooked(3, 5, 10f, 15f);

            Assert.That(_evidence.Describe(3), Is.EqualTo(
                "{\"type\":\"look\",\"ago\":\"0.000\",\"tick\":\"5\",\"yaw\":\"10\",\"pitch\":\"15\"}\n"));
        }

        [Test]
        public void BotsAreNotHeld()
        {
            Start(true, 30f);
            var bot = BotRoster.FirstBotPlayerId;

            _signals.RaiseShotAccepted(bot, Vector3.Zero, Vector3.UnitZ, 1f);
            _signals.RaiseHit(bot, 1, HitZone.Body, 0f);
            _signals.RaiseLooked(bot, 1, 0f, null);

            Assert.That(_evidence.HeldPlayerCount, Is.Zero);
            Assert.That(_evidence.Describe(bot), Is.Empty);
        }

        [Test]
        public void LeavingDropsThePlayersRecords()
        {
            Start(true, 30f);
            _signals.RaiseLooked(1, 1, 0f, null);
            _signals.RaiseLooked(2, 1, 0f, null);

            _evidence.OnDisconnected(new ConnectionId(1));

            Assert.That(_evidence.HeldPlayerCount, Is.EqualTo(1));
            Assert.That(_evidence.Describe(1), Is.Empty);
            Assert.That(_evidence.Describe(2), Is.Not.Empty);
        }

        [Test]
        public void DisabledEvidenceHoldsNothingAndDescribesNothing()
        {
            Start(false, 30f);

            _signals.RaiseLooked(1, 1, 0f, null);
            _signals.RaiseShotAccepted(1, Vector3.Zero, Vector3.UnitZ, 1f);

            Assert.That(_evidence.HeldPlayerCount, Is.Zero);
            Assert.That(_evidence.Describe(1), Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Evidence] evidence is off"));
        }

        [Test]
        public void StopStopsListeningAndForgetsEverything()
        {
            Start(true, 30f);
            Assert.That(_log.ToString(), Does.Contain("[Evidence] holding the last 30s of each player for reports"));
            _signals.RaiseLooked(1, 1, 0f, null);

            _evidence.Stop();
            _signals.RaiseLooked(1, 2, 0f, null);

            Assert.That(_evidence.HeldPlayerCount, Is.Zero);
            Assert.That(_evidence.Describe(1), Is.Null);
        }

        [Test]
        public void TheRingKeepsTheNewestRecordsWhenItIsFull()
        {
            // 0.01 秒 × 毎秒 240 件で、1 人 3 件まで
            Start(true, 0.01f);

            for (var tick = 1; tick <= 5; tick++)
            {
                _signals.RaiseLooked(1, tick, 0f, null);
            }

            var lines = _evidence.Describe(1).TrimEnd('\n').Split('\n');
            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines[0], Does.Contain("\"tick\":\"3\""));
            Assert.That(lines[2], Does.Contain("\"tick\":\"5\""));
        }

        [Test]
        public void TheRingGrowsPastItsFirstSizeInOrder()
        {
            Start(true, 1f);

            for (var tick = 1; tick <= 100; tick++)
            {
                _signals.RaiseLooked(1, tick, 0f, null);
            }

            var lines = _evidence.Describe(1).TrimEnd('\n').Split('\n');
            Assert.That(lines, Has.Length.EqualTo(100));
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.That(lines[i], Does.Contain("\"tick\":\"" + (i + 1) + "\""));
            }
        }

        [Test]
        public void TheConstructorRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerReportEvidence(null, AntiCheatTuning.EvidenceSettings.Default));
            Assert.Throws<ArgumentNullException>(() => new ServerReportEvidence(_signals, null));
            Assert.That(ServerReportEvidence.MaxRecordsPerSecond, Is.EqualTo(240));
        }

        private void Start(bool enabled, float seconds)
        {
            _evidence = new ServerReportEvidence(_signals, new AntiCheatTuning.EvidenceSettings(enabled, seconds));
            _evidence.Start(_context);
        }

        private void Advance(float seconds)
        {
            _evidence.Tick(++_tick, seconds);
        }
    }
}
