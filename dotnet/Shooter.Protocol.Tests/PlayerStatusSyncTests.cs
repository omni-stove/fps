using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    // スタミナ・よろけ・制圧は持ち主にだけ送り、変わった部分だけを差分に載せることを確かめる
    public sealed class PlayerStatusSyncTests
    {
        // 試合の部分が空の差分の、持ち主の部分の頭 (Id・版・tick・基準の tick・試合の部分のビット・持ち主の有無・持ち主の部分のビット・最後に処理した入力の tick)
        private const int _ownerBodyAt = 2 + 1 + 4 + 4 + 2 + 1 + 2 + 4;

        [Test]
        public void ThePartsAreTheNextThreeBits()
        {
            Assert.That((int)OwnerFields.Stamina, Is.EqualTo(1 << 9));
            Assert.That((int)OwnerFields.Stagger, Is.EqualTo(1 << 10));
            Assert.That((int)OwnerFields.Suppression, Is.EqualTo(1 << 11));
            Assert.That(OwnerFields.All.HasFlag(OwnerFields.Stamina | OwnerFields.Stagger | OwnerFields.Suppression), Is.True);
        }

        [Test]
        public void TheStatusRoundTrips()
        {
            var sent = Snapshot(10);
            sent.Owner.Stamina = new StaminaStatus(37.5f, 100f, true);
            sent.Owner.Stagger = StaggerKind.Blast;
            sent.Owner.SuppressionLevel = 0.625f;
            sent.Owner.Suppressed = true;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Owner.Stamina, Is.EqualTo(new StaminaStatus(37.5f, 100f, true)));
            Assert.That(got.Owner.Stagger, Is.EqualTo(StaggerKind.Blast));
            Assert.That(got.Owner.SuppressionLevel, Is.EqualTo(0.625f));
            Assert.That(got.Owner.Suppressed, Is.True);
        }

        [Test]
        public void StaminaThatIsOffRoundTripsAsNone()
        {
            var got = Send(Snapshot(10), null);

            Assert.That(got.Owner.Stamina, Is.Null);
            Assert.That(got.Owner.Stagger, Is.EqualTo(StaggerKind.None));
            Assert.That(got.Owner.SuppressionLevel, Is.Zero);
            Assert.That(got.Owner.Suppressed, Is.False);
        }

        [Test]
        public void ChangedStaminaSendsOnlyItsBytesAndResolves()
        {
            var baseline = Snapshot(10);
            baseline.Owner.Stamina = new StaminaStatus(100f, 100f, false);
            baseline.Owner.Stagger = StaggerKind.Stumble;
            baseline.Owner.SuppressionLevel = 0.25f;
            var unchanged = Snapshot(11);
            unchanged.Owner.CopyFrom(baseline.Owner);
            unchanged.Owner.LastProcessedInputTick = 10;
            var current = Snapshot(11);
            current.Owner.CopyFrom(unchanged.Owner);
            current.Owner.Stamina = new StaminaStatus(99.5f, 100f, false);

            var got = Send(current, baseline);

            // 有無 (1)・量 (4)・最大 (4)・尽きたか (1)
            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(1 + 4 + 4 + 1));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.Stamina, Is.EqualTo(new StaminaStatus(99.5f, 100f, false)));
            Assert.That(got.Owner.Stagger, Is.EqualTo(StaggerKind.Stumble), "the unchanged stagger comes from the baseline");
            Assert.That(got.Owner.SuppressionLevel, Is.EqualTo(0.25f), "the unchanged suppression comes from the baseline");
        }

        [Test]
        public void ChangedStaggerAndSuppressionSendOnlyTheirBytes()
        {
            var baseline = Snapshot(10);
            baseline.Owner.Stamina = new StaminaStatus(50f, 100f, false);
            var unchanged = Snapshot(11);
            unchanged.Owner.CopyFrom(baseline.Owner);
            unchanged.Owner.LastProcessedInputTick = 10;
            var staggered = Snapshot(11);
            staggered.Owner.CopyFrom(unchanged.Owner);
            staggered.Owner.Stagger = StaggerKind.Kneel;
            var suppressed = Snapshot(11);
            suppressed.Owner.CopyFrom(unchanged.Owner);
            suppressed.Owner.SuppressionLevel = 0.5f;
            suppressed.Owner.Suppressed = true;

            var gotStaggered = Send(staggered, baseline);
            var gotSuppressed = Send(suppressed, baseline);

            Assert.That(Size(staggered, baseline) - Size(unchanged, baseline), Is.EqualTo(1));
            Assert.That(Size(suppressed, baseline) - Size(unchanged, baseline), Is.EqualTo(4 + 1));
            Assert.That(gotStaggered.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotStaggered.Owner.Stagger, Is.EqualTo(StaggerKind.Kneel));
            Assert.That(gotStaggered.Owner.Stamina, Is.EqualTo(new StaminaStatus(50f, 100f, false)));
            Assert.That(gotSuppressed.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotSuppressed.Owner.SuppressionLevel, Is.EqualTo(0.5f));
            Assert.That(gotSuppressed.Owner.Suppressed, Is.True);
        }

        [Test]
        public void TurningStaminaOffIsSent()
        {
            var baseline = Snapshot(10);
            baseline.Owner.Stamina = new StaminaStatus(20f, 100f, false);
            var current = Snapshot(11);

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.Stamina, Is.Null);
        }

        [Test]
        public void ClearingAndCopyingASnapshotHandleTheStatus()
        {
            var sent = Snapshot(10);
            sent.Owner.Stamina = new StaminaStatus(1f, 2f, true);
            sent.Owner.Stagger = StaggerKind.Flinch;
            sent.Owner.SuppressionLevel = 0.75f;
            sent.Owner.Suppressed = true;
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.Owner.Stamina, Is.EqualTo(new StaminaStatus(1f, 2f, true)));
            Assert.That(copy.Owner.Stagger, Is.EqualTo(StaggerKind.Flinch));
            Assert.That(copy.Owner.SuppressionLevel, Is.EqualTo(0.75f));
            Assert.That(copy.Owner.Suppressed, Is.True);
            Assert.That(sent.Owner.Stamina, Is.Null);
            Assert.That(sent.Owner.Stagger, Is.EqualTo(StaggerKind.None));
            Assert.That(sent.Owner.SuppressionLevel, Is.Zero);
            Assert.That(sent.Owner.Suppressed, Is.False);
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void AnImpossibleSuppressionLevelIsNotWritten(float level)
        {
            var sent = Snapshot(10);
            sent.Owner.SuppressionLevel = level;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnUnknownStaggerIsNotWritten()
        {
            var sent = Snapshot(10);
            sent.Owner.Stagger = (StaggerKind)5;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void DefaultStaminaIsNotWritten()
        {
            var sent = Snapshot(10);
            sent.Owner.Stamina = default(StaminaStatus);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnUnknownStaggerIsUnreadable()
        {
            var baseline = Snapshot(10);
            var current = Snapshot(11);
            current.Owner.LastProcessedInputTick = 9;
            current.Owner.Stagger = StaggerKind.Kneel;
            var bytes = Bytes(current, baseline);
            Assert.That(bytes[_ownerBodyAt], Is.EqualTo((byte)StaggerKind.Kneel));

            bytes[_ownerBodyAt] = (byte)StaggerKind.Blast + 1;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void StaminaAboveItsMaxIsUnreadable()
        {
            var baseline = Snapshot(10);
            var current = Snapshot(11);
            current.Owner.LastProcessedInputTick = 9;
            current.Owner.Stamina = new StaminaStatus(10f, 100f, false);
            var bytes = Bytes(current, baseline);

            // 有無 (1) の後に量 (4) が並ぶ
            BitConverter.TryWriteBytes(new Span<byte>(bytes, _ownerBodyAt + 1, 4), 200f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ASuppressionLevelAboveOneIsUnreadable()
        {
            var baseline = Snapshot(10);
            var current = Snapshot(11);
            current.Owner.LastProcessedInputTick = 9;
            current.Owner.SuppressionLevel = 0.5f;
            var bytes = Bytes(current, baseline);

            BitConverter.TryWriteBytes(new Span<byte>(bytes, _ownerBodyAt, 4), 1.5f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static SnapshotMessage Snapshot(int tick)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = 9;
            snapshot.Match.Rules = MatchRulesSync.From(GameRules.Default);
            snapshot.Players.Add().PlayerId = 1;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(Bytes(message, baseline))));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            return Bytes(message, baseline).Length;
        }

        private static byte[] Bytes(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.ToArray();
        }
    }
}
