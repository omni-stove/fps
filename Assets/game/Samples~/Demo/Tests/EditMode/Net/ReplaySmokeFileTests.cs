using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    public sealed class ReplaySmokeFileTests
    {
        private const string Version = "0.2.0";
        private const string Map = "NetArena01";

        private static readonly DateTime _recordedAt = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        private readonly List<string> _logs = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
        }

        [Test]
        public void TheHeaderNamesTheMapAndLastsTheGivenSeconds()
        {
            var file = ReplaySmokeFile.Build(Version, Map, 3f, _recordedAt);

            using (var reader = new ReplayReader(new MemoryStream(file)))
            {
                var header = reader.Header;
                Assert.That(header.ProtocolVersion, Is.EqualTo(MatchProtocol.Version));
                Assert.That(header.ClientVersion, Is.EqualTo(Version));
                Assert.That(header.MapId, Is.EqualTo(Map));
                Assert.That(header.PlayerId, Is.EqualTo(1));
                Assert.That(header.TickRate, Is.EqualTo(30));
                Assert.That(header.RecordedAtUtc, Is.EqualTo(_recordedAt));
                Assert.That(header.DurationTicks, Is.EqualTo(90));
                Assert.That(reader.IsTruncated, Is.False);
            }

            var entry = ReplayFile.ReadEntry("smoke", new MemoryStream(file), out _);
            Assert.That(entry.MapId, Is.EqualTo(Map));
            Assert.That(entry.DurationSeconds, Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void PlaysBackTheConnectTheAcceptAndTheEnd()
        {
            var file = ReplaySmokeFile.Build(Version, Map, 3f, _recordedAt);

            var played = new List<(int Tick, TransportEventKind Kind, string Reason)>();
            using (var replay = new ReplayTransport(new MemoryStream(file), _logs.Add))
            {
                replay.Connect(string.Empty, 0);
                for (var tick = 0; tick <= 100 && replay.IsActive; tick++)
                {
                    while (replay.TryReceive(out var e))
                    {
                        played.Add((tick, e.Kind, e.Kind == TransportEventKind.Disconnected ? e.Reason : ""));
                        if (e.Kind == TransportEventKind.Data)
                        {
                            Assert.That(Bytes(e.Payload), Is.EqualTo(ReplaySmokeFile.Accepted(Version, Map)));
                        }
                    }

                    replay.Advance(1f / 30f);
                }

                Assert.That(replay.IsEnded, Is.True);
            }

            Assert.That(played, Is.EqualTo(new[]
            {
                (0, TransportEventKind.Connected, ""),
                (3, TransportEventKind.Data, ""),
                (90, TransportEventKind.Disconnected, "server closed"),
            }));
            Assert.That(_logs, Does.Contain(ReplayLog.PlaybackEnded(3, false)));
        }

        [Test]
        public void TheAcceptGivesTheMapAndTheTickRate()
        {
            var reader = new MessageReader(ReplaySmokeFile.Accepted(Version, Map));
            Assert.That(reader.ReadMessageId(), Is.EqualTo(MessageIds.ConnectAccepted));
            var message = new ConnectAcceptedMessage();
            reader.ReadMessageBody(message);

            Assert.That(message.PlayerId, Is.EqualTo(1));
            Assert.That(message.ServerVersion, Is.EqualTo(Version));
            Assert.That(message.Map, Is.EqualTo(Map));
            Assert.That(message.TickRate, Is.EqualTo(30));
            Assert.That(message.MaxHealth, Is.EqualTo(100));
        }

        private static byte[] Bytes(ArraySegment<byte> segment)
        {
            var bytes = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            return bytes;
        }

        [TestCase(null, Map, 3f)]
        [TestCase(" ", Map, 3f)]
        [TestCase(Version, null, 3f)]
        [TestCase(Version, "", 3f)]
        public void RefusesAMissingVersionOrMap(string version, string map, float seconds)
        {
            Assert.Throws<ArgumentException>(() => ReplaySmokeFile.Build(version, map, seconds, _recordedAt));
        }

        [TestCase(0.1f)]
        [TestCase(0f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void RefusesALengthThatDoesNotOutlastTheAccept(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ReplaySmokeFile.Build(Version, Map, seconds, _recordedAt));
        }

        [Test]
        public void ClassifiesThePlaybackLinesItWaitsFor()
        {
            Assert.That(ReplaySmokeRun.Classify("[Replay] playing NetArena01 (player 1)"), Is.EqualTo(ReplaySmokeRun.LineKind.Playing));
            Assert.That(
                ReplaySmokeRun.Classify("[Net] client connected id=1 (local) server version=0.2.0 map=NetArena01 tickRate=30"),
                Is.EqualTo(ReplaySmokeRun.LineKind.Accepted));
            Assert.That(ReplaySmokeRun.Classify(ReplayLog.PlaybackEnded(3, false)), Is.EqualTo(ReplaySmokeRun.LineKind.Ended));
            Assert.That(ReplaySmokeRun.Classify(ReplayLog.PlaybackEnded(3, true)), Is.EqualTo(ReplaySmokeRun.LineKind.Ended));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(ReplaySmokeRun.Classify(null), Is.EqualTo(ReplaySmokeRun.LineKind.Other));
            Assert.That(ReplaySmokeRun.Classify(ReplayLog.PlaybackStopped("left")), Is.EqualTo(ReplaySmokeRun.LineKind.Other));
            Assert.That(ReplaySmokeRun.Classify(ReplayLog.PlaybackFailed("bad record")), Is.EqualTo(ReplaySmokeRun.LineKind.Other));
            Assert.That(
                ReplaySmokeRun.Classify("[ReplaySmokeRun] [Replay] playback reached the end after 3 events"),
                Is.EqualTo(ReplaySmokeRun.LineKind.Other));
        }
    }
}
