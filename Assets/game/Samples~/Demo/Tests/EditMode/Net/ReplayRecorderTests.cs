using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    public sealed class ReplayRecorderTests
    {
        private const int TickRate = 10;
        private const int PlayerId = 3;
        private const string Map = "Arena";
        private const string ClientVersion = "0.2.0";

        private static readonly DateTime _recordedAt = new DateTime(2026, 11, 18, 12, 30, 0, DateTimeKind.Utc);
        private static readonly ConnectionId _server = new ConnectionId(7);

        private readonly List<string> _logs = new List<string>();
        private QueueTransport _inner;
        private double _now;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _inner = new QueueTransport();
            _now = 0d;
        }

        [Test]
        public void RecordedMessagesPlayBackInTheSameOrderAtTheSameTick()
        {
            var file = RecordMatch();

            // 10 tick/s で、届いた時刻 (秒) × 10 を切り捨てた tick になる
            var expected = new[]
            {
                (Tick: 0, Kind: TransportEventKind.Connected, Channel: TransportChannel.Reliable, Bytes: "", Reason: ""),
                (Tick: 1, Kind: TransportEventKind.Data, Channel: TransportChannel.Reliable, Bytes: Hex(Accepted()), Reason: ""),
                (Tick: 5, Kind: TransportEventKind.Data, Channel: TransportChannel.Unreliable, Bytes: "090807", Reason: ""),
                (Tick: 5, Kind: TransportEventKind.Data, Channel: TransportChannel.Reliable, Bytes: "0102", Reason: ""),
                (Tick: 10, Kind: TransportEventKind.Data, Channel: TransportChannel.Reliable, Bytes: "05", Reason: ""),
                (Tick: 20, Kind: TransportEventKind.Disconnected, Channel: TransportChannel.Reliable, Bytes: "", Reason: "server closed"),
            };

            var played = new List<(int Tick, TransportEventKind Kind, TransportChannel Channel, string Bytes, string Reason)>();
            using (var replay = new ReplayTransport(new MemoryStream(file), _logs.Add))
            {
                replay.Connect("ignored", 0);
                for (var tick = 0; tick <= 25 && replay.IsActive; tick++)
                {
                    while (replay.TryReceive(out var e))
                    {
                        played.Add((tick, e.Kind, e.Kind == TransportEventKind.Data ? e.Channel : TransportChannel.Reliable,
                            e.Kind == TransportEventKind.Data ? Hex(e.Payload) : "", e.Kind == TransportEventKind.Disconnected ? e.Reason : ""));
                        Assert.That(e.Connection, Is.EqualTo(_server), "the replay keeps the recorded connection");
                    }

                    replay.Advance(1f / TickRate);
                }

                Assert.That(replay.IsEnded, Is.True);
                Assert.That(replay.PlayedEvents, Is.EqualTo(6));
            }

            Assert.That(played, Is.EqualTo(expected));
        }

        [Test]
        public void TheFileHoldsTheMatchHeaderAndTheTickOfEveryRecord()
        {
            var file = RecordMatch();

            using (var reader = new ReplayReader(new MemoryStream(file)))
            {
                var header = reader.Header;
                Assert.That(header.ProtocolVersion, Is.EqualTo(MatchProtocol.Version));
                Assert.That(header.ClientVersion, Is.EqualTo(ClientVersion));
                Assert.That(header.MapId, Is.EqualTo(Map));
                Assert.That(header.PlayerId, Is.EqualTo(PlayerId));
                Assert.That(header.TickRate, Is.EqualTo(TickRate));
                Assert.That(header.RecordedAtUtc, Is.EqualTo(_recordedAt));
                Assert.That(header.DurationTicks, Is.EqualTo(20), "the recorder writes the length back when the match ends");

                var ticks = new List<int>();
                var kinds = new List<ReplayRecordKind>();
                while (reader.TryRead(out var record))
                {
                    ticks.Add(record.Tick);
                    kinds.Add(record.Kind);
                    Assert.That(record.Connection, Is.EqualTo(_server.Value));
                }

                Assert.That(ticks, Is.EqualTo(new[] { 0, 1, 5, 5, 10, 20 }));
                Assert.That(kinds, Is.EqualTo(new[]
                {
                    ReplayRecordKind.Connected, ReplayRecordKind.Data, ReplayRecordKind.Data,
                    ReplayRecordKind.Data, ReplayRecordKind.Data, ReplayRecordKind.Disconnected,
                }));
                Assert.That(reader.IsTruncated, Is.False);
            }
        }

        [Test]
        public void TheRecorderPassesEveryEventThroughUnchanged()
        {
            var output = new MemoryStream();
            var recorder = NewRecorder(output);
            recorder.Connect("127.0.0.1", 7777);
            _inner.Enqueue(TransportEvent.Connected(_server));
            _inner.Enqueue(TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted())));
            _inner.Enqueue(TransportEvent.Data(_server, TransportChannel.Unreliable, new ArraySegment<byte>(new byte[] { 4, 2 })));

            var passed = new List<string>();
            while (recorder.TryReceive(out var e))
            {
                passed.Add(e.Kind + ":" + (e.Kind == TransportEventKind.Data ? e.Channel + ":" + Hex(e.Payload) : ""));
            }

            Assert.That(passed, Is.EqualTo(new[]
            {
                "Connected:", "Data:Reliable:" + Hex(Accepted()), "Data:Unreliable:0402",
            }));
            Assert.That(_inner.Connected, Is.True, "Connect goes to the real transport");

            recorder.Send(_server, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 1 }));
            Assert.That(_inner.SentCount, Is.EqualTo(1), "what the client sends goes to the real transport and is not recorded");

            recorder.Dispose();
            Assert.That(_inner.Disposed, Is.True);
            Assert.That(recorder.State, Is.EqualTo(ReplayRecorderState.Finished));
            Assert.That(recorder.RecordedEvents, Is.EqualTo(3));
        }

        [Test]
        public void AConnectionThatIsNeverAcceptedLeavesNothingToPlay()
        {
            var output = new MemoryStream();
            var recorder = NewRecorder(output);
            recorder.Connect("127.0.0.1", 7777);
            _inner.Enqueue(TransportEvent.Connected(_server));
            _inner.Enqueue(TransportEvent.Disconnected(_server, "server full"));
            while (recorder.TryReceive(out _))
            {
            }

            Assert.That(recorder.State, Is.EqualTo(ReplayRecorderState.Abandoned));
            Assert.That(recorder.HasRecording, Is.False, "the caller deletes the empty file");
            Assert.That(output.ToArray(), Is.Empty);
            Assert.That(_logs.Any(line => line.StartsWith(ReplayLog.Tag + " not recording", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void TooManyEventsBeforeTheAcceptStopRecordingButNotTheMatch()
        {
            var output = new MemoryStream();
            var recorder = new ReplayRecorder(_inner, output, ClientVersion, _logs.Add, () => _now, () => _recordedAt, 2);
            recorder.Connect("127.0.0.1", 7777);
            for (var i = 0; i < 3; i++)
            {
                _inner.Enqueue(TransportEvent.Data(_server, TransportChannel.Unreliable, new ArraySegment<byte>(new byte[] { (byte)i })));
            }

            _inner.Enqueue(TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted())));
            var passed = 0;
            while (recorder.TryReceive(out _))
            {
                passed++;
            }

            Assert.That(passed, Is.EqualTo(4), "the match still receives everything");
            Assert.That(recorder.State, Is.EqualTo(ReplayRecorderState.Abandoned));
            Assert.That(recorder.HasRecording, Is.False);
        }

        [Test]
        public void AFileCutOffByACrashPlaysUpToTheCutAndThenEnds()
        {
            var output = new MemoryStream();
            var recorder = NewRecorder(output);
            recorder.Connect("127.0.0.1", 7777);
            Receive(recorder, 0.0, TransportEvent.Connected(_server));
            Receive(recorder, 0.1, TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted())));
            Receive(recorder, 0.5, TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 })));

            // 閉じる前 (長さを書き戻す前) の中身を、最後の記録の途中で切る
            var whole = output.ToArray();
            var cut = whole.Take(whole.Length - 2).ToArray();

            using (var reader = new ReplayReader(new MemoryStream(cut)))
            {
                Assert.That(reader.Header.HasDuration, Is.False, "the length was never written back");
            }

            Assert.That(ReplayFile.ReadEntry("cut", new MemoryStream(cut), out _).DurationSeconds, Is.EqualTo(0.1f).Within(1e-5f),
                "without a written length the list counts up to the last whole record");

            var kinds = new List<TransportEventKind>();
            string reason = null;
            using (var replay = new ReplayTransport(new MemoryStream(cut), _logs.Add))
            {
                replay.Connect("ignored", 0);
                replay.Advance(10f);
                while (replay.TryReceive(out var e))
                {
                    kinds.Add(e.Kind);
                    if (e.Kind == TransportEventKind.Disconnected)
                    {
                        reason = e.Reason;
                    }
                }
            }

            Assert.That(kinds, Is.EqualTo(new[] { TransportEventKind.Connected, TransportEventKind.Data, TransportEventKind.Disconnected }));
            Assert.That(reason, Is.EqualTo(ReplayTransport.EndedReason));
            Assert.That(_logs.Any(line => line.Contains("cut off")), Is.True);
            recorder.Dispose();
        }

        [Test]
        public void AReplayFromAnotherProtocolIsRefusedBeforePlaying()
        {
            var output = new MemoryStream();
            var header = new ReplayHeader((ushort)(MatchProtocol.Version + 1), "9.9.9", Map, _recordedAt, TickRate, PlayerId);
            using (var writer = new ReplayWriter(output, header, true))
            {
                writer.Append(0, ReplayRecordKind.Connected, _server.Value, 0, default);
            }

            var error = Assert.Throws<InvalidDataException>(() => new ReplayTransport(new MemoryStream(output.ToArray()), _logs.Add));
            Assert.That(error.Message, Does.Contain("9.9.9"));
        }

        [Test]
        public void PausingHoldsTheEventsAndSpeedMovesTheClockFaster()
        {
            var file = RecordMatch();
            using (var replay = new ReplayTransport(new MemoryStream(file), _logs.Add))
            {
                replay.Connect("ignored", 0);
                Assert.That(replay.TryReceive(out var first), Is.True);
                Assert.That(first.Kind, Is.EqualTo(TransportEventKind.Connected));

                replay.Paused = true;
                replay.Advance(5f);
                Assert.That(replay.TryReceive(out _), Is.False, "nothing moves while paused");

                replay.Paused = false;
                replay.Speed = 4f;
                replay.Advance(0.5f);
                Assert.That(replay.PlaybackTick, Is.EqualTo(20d).Within(1e-6), "0.5 s at 4x is 2 s of the match (20 ticks)");
                Assert.Throws<ArgumentOutOfRangeException>(() => replay.Speed = 0f);

                replay.Send(_server, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 1 }));
                Assert.That(replay.TryGetRoundTripTime(_server, out _), Is.False);
            }
        }

        [Test]
        public void AMatchClientReadingTheReplayIsAcceptedOnTheRecordedMapAndLeavesAtTheEnd()
        {
            var file = RecordMatch();
            var replay = new ReplayTransport(new MemoryStream(file), _logs.Add);
            using (var client = new MatchClient(replay, ClientVersion, RejoinKey.NewDeviceKey(), "viewer", _logs.Add))
            {
                var accepted = 0;
                DisconnectNotice notice = null;
                client.Accepted += () => accepted++;
                client.Disconnected += n => notice = n;
                client.Connect("replay", 0);
                for (var i = 0; i < 30; i++)
                {
                    client.Poll();
                    replay.Advance(1f / TickRate);
                }

                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(client.Map, Is.EqualTo(Map));
                Assert.That(client.PlayerId, Is.EqualTo(PlayerId));
                Assert.That(client.TickRate, Is.EqualTo(TickRate));
                Assert.That(client.State, Is.EqualTo(MatchClientState.Disconnected));
                Assert.That(notice, Is.Not.Null);
            }
        }

        private byte[] RecordMatch()
        {
            var output = new MemoryStream();
            var recorder = NewRecorder(output);
            recorder.Connect("127.0.0.1", 7777);
            Receive(recorder, 0.05, TransportEvent.Connected(_server));
            Receive(recorder, 0.10, TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(Accepted())));
            Receive(recorder, 0.50, TransportEvent.Data(_server, TransportChannel.Unreliable, new ArraySegment<byte>(new byte[] { 9, 8, 7 })));
            Receive(recorder, 0.51, TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 1, 2 })));
            Receive(recorder, 1.00, TransportEvent.Data(_server, TransportChannel.Reliable, new ArraySegment<byte>(new byte[] { 5 })));
            Receive(recorder, 2.00, TransportEvent.Disconnected(_server, "server closed"));
            Assert.That(recorder.State, Is.EqualTo(ReplayRecorderState.Finished), "the server's disconnect ends the recording");
            _now = 3.0;
            recorder.Dispose();
            return output.ToArray();
        }

        private ReplayRecorder NewRecorder(Stream output)
        {
            return new ReplayRecorder(_inner, output, ClientVersion, _logs.Add, () => _now, () => _recordedAt);
        }

        private void Receive(ReplayRecorder recorder, double seconds, TransportEvent transportEvent)
        {
            _now = seconds;
            _inner.Enqueue(transportEvent);
            Assert.That(recorder.TryReceive(out _), Is.True);
        }

        private static byte[] Accepted()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage
            {
                PlayerId = PlayerId,
                ServerVersion = ClientVersion,
                TickRate = TickRate,
                ServerTick = 100,
                Map = Map,
                MaxHealth = 100,
            });
            return writer.ToArray();
        }

        private static string Hex(byte[] bytes)
        {
            return Hex(new ArraySegment<byte>(bytes));
        }

        private static string Hex(ArraySegment<byte> bytes)
        {
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        private sealed class QueueTransport : IMatchTransport
        {
            private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();

            public bool IsActive => Connected && !Disposed;

            public bool Connected { get; private set; }

            public bool Disposed { get; private set; }

            public int SentCount { get; private set; }

            public void Enqueue(TransportEvent transportEvent)
            {
                _events.Enqueue(transportEvent);
            }

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
                Connected = true;
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
                SentCount++;
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                if (_events.Count == 0)
                {
                    transportEvent = default;
                    return false;
                }

                transportEvent = _events.Dequeue();
                return true;
            }

            public void Shutdown(string reason)
            {
                Connected = false;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
