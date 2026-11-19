using System;
using NUnit.Framework;

namespace Shooter.Protocol.Tests
{
    public sealed class RespawnSyncTests
    {
        [Test]
        public void TheOwnersRespawnCountdownRoundTrips()
        {
            var got = Send(Snapshot(10, 4), null);

            Assert.That(got.Owner.RespawnSeconds, Is.EqualTo(4));
        }

        [Test]
        public void NoCountdownRoundTripsAsNone()
        {
            var got = Send(Snapshot(10, null), null);

            Assert.That(got.Owner.RespawnSeconds, Is.Null);
        }

        [Test]
        public void ZeroSecondsIsStillACountdown()
        {
            var got = Send(Snapshot(10, 0), null);

            Assert.That(got.Owner.RespawnSeconds, Is.EqualTo(0));
        }

        [Test]
        public void AnUnchangedCountdownIsFilledFromTheBaselineAndAChangeIsSent()
        {
            var baseline = Snapshot(10, 5);
            var unchanged = Snapshot(11, 5);
            var ticked = Snapshot(12, 4);
            var respawned = Snapshot(13, null);

            var gotUnchanged = Send(unchanged, baseline);
            var gotTicked = Send(ticked, baseline);
            var gotRespawned = Send(respawned, baseline);

            Assert.That(gotUnchanged.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotUnchanged.Owner.RespawnSeconds, Is.EqualTo(5));
            Assert.That(gotTicked.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotTicked.Owner.RespawnSeconds, Is.EqualTo(4));
            Assert.That(gotRespawned.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotRespawned.Owner.RespawnSeconds, Is.Null);
            // 変わらなければ出直しの欄 (有無の 1 バイトと秒の 4 バイト) を載せない
            Assert.That(Size(unchanged, baseline), Is.EqualTo(Size(ticked, baseline) - (1 + 4)));
        }

        [Test]
        public void ClearingASnapshotForgetsTheCountdown()
        {
            var snapshot = Snapshot(10, 3);

            snapshot.Clear();

            Assert.That(snapshot.Owner.RespawnSeconds, Is.Null);
        }

        [Test]
        public void CopyingASnapshotKeepsTheCountdown()
        {
            var copy = new SnapshotMessage();

            copy.CopyFrom(Snapshot(10, 7));

            Assert.That(copy.Owner.RespawnSeconds, Is.EqualTo(7));
        }

        [Test]
        public void ANegativeCountdownIsNotWritten()
        {
            var writer = new MessageWriter();

            Assert.Throws<InvalidOperationException>(() => writer.WriteMessage(Snapshot(10, -1)));
        }

        [Test]
        public void ANegativeCountdownIsUnreadable()
        {
            const int marker = 0x01234567;
            var writer = new MessageWriter();
            writer.WriteMessage(Snapshot(10, marker));
            var bytes = writer.AsSegment().ToArray();
            var at = IndexOf(bytes, BitConverter.GetBytes(marker));
            BitConverter.TryWriteBytes(new Span<byte>(bytes, at, 4), -1);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static SnapshotMessage Snapshot(int tick, int? respawnSeconds)
        {
            var snapshot = new SnapshotMessage { Tick = tick, HasOwner = true };
            snapshot.Owner.LastProcessedInputTick = tick - 1;
            snapshot.Owner.RespawnSeconds = respawnSeconds;
            snapshot.Players.Add().PlayerId = 1;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment()));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.Length;
        }

        private static int IndexOf(byte[] bytes, byte[] pattern)
        {
            for (var i = bytes.Length - pattern.Length; i >= 0; i--)
            {
                if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }

            throw new AssertionException("pattern not found");
        }
    }
}
