using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2ReplaysTests
    {
        private readonly List<string> _logs = new List<string>();
        private readonly List<string> _played = new List<string>();
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _played.Clear();
            NetV2Replays.Uninstall();
            _directory = Path.Combine(Path.GetTempPath(), "shooter-netv2-replays-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            NetV2Replays.Uninstall();
            GameSignals.Replays.Clear();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void InstallPublishesTheLibraryOnce()
        {
            var library = NetV2Replays.Install(_directory, Play);

            Assert.That(GameSignals.Replays.Value, Is.SameAs(library));
            Assert.That(NetV2Replays.Install(_directory, Play), Is.SameAs(library), "a second install keeps the first library");
            Assert.That(library.Directory, Is.EqualTo(_directory));
            Assert.That(library.Entries, Is.Empty);

            NetV2Replays.Uninstall();

            Assert.That(GameSignals.Replays.HasValue, Is.False);
            Assert.That(NetV2Replays.Library, Is.Null);
        }

        [Test]
        public void WithoutALibraryTheMatchIsNotRecorded()
        {
            var transport = new SilentTransport();

            var used = NetV2Replays.Record(transport, "0.2.0", _logs.Add, out var recorder, out var path);

            Assert.That(used, Is.SameAs(transport));
            Assert.That(recorder, Is.Null);
            Assert.That(path, Is.Null);
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void WithALibraryTheTransportIsWrappedByARecorderThatWritesIntoTheFolder()
        {
            NetV2Replays.Install(_directory, Play);
            var transport = new SilentTransport();

            var used = NetV2Replays.Record(transport, "0.2.0", _logs.Add, out var recorder, out var path);
            try
            {
                Assert.That(used, Is.SameAs(recorder));
                Assert.That(recorder.State, Is.EqualTo(ReplayRecorderState.Waiting));
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(_directory));
                Assert.That(Path.GetExtension(path), Is.EqualTo(ReplayFile.Extension));
                Assert.That(File.Exists(path), Is.True);
                Assert.That(_logs, Is.EqualTo(new[] { ReplayLog.RecordingTo(path) }));
            }
            finally
            {
                recorder?.Dispose();
            }
        }

        [Test]
        public void AMatchThatEndedBeforeItWasAcceptedLeavesNoFile()
        {
            NetV2Replays.Install(_directory, Play);
            NetV2Replays.Record(new SilentTransport(), "0.2.0", _logs.Add, out var recorder, out var path);
            _logs.Clear();

            recorder.Dispose();
            NetV2Replays.Kept(recorder, path, _logs.Add);

            Assert.That(File.Exists(path), Is.False);
            Assert.That(_logs, Does.Contain(ReplayLog.DiscardedEmpty(path)));
            Assert.That(NetV2Replays.Library.Entries, Is.Empty);
        }

        [Test]
        public void KeptWithoutARecordingDoesNothing()
        {
            Assert.DoesNotThrow(() => NetV2Replays.Kept(null, null, _logs.Add));
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void RefusesMissingPieces()
        {
            Assert.Throws<ArgumentNullException>(() => NetV2Replays.Record(null, "0.2.0", _logs.Add, out _, out _));
            Assert.Throws<ArgumentNullException>(() => NetV2Replays.Record(new SilentTransport(), "0.2.0", null, out _, out _));
        }

        [Test]
        public void BeginReplayRefusesAFileThatIsNotAReplay()
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "broken" + ReplayFile.Extension);
            File.WriteAllText(path, "not a replay");

            Assert.That(() => NetV2Client.BeginReplay(path), Throws.InstanceOf<InvalidDataException>().Or.InstanceOf<EndOfStreamException>());
            Assert.That(NetV2Client.Current, Is.Null, "no client is made for a file that cannot be read");
            Assert.Throws<ArgumentException>(() => NetV2Client.BeginReplay(" "));
        }

        private bool Play(string path)
        {
            _played.Add(path);
            return true;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
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
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
