using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class SnapshotRoundResultTests
    {
        // ラウンドの番号と勝ちに要る数 (4 バイトずつ)、前のラウンドの有無と終わり方・勝者の有無と勝者 (1 バイトずつ)
        private const int _roundResultBytes = 4 + 4 + 1 + 1 + 1 + 1;

        // 設置の割合・解除している人・解除の割合 (4 バイトずつ)
        private const int _progressBytes = 4 + 4 + 4;

        // 試合の部分のビットまでの頭 (Id・版・tick・基準の tick・部分のビット)
        private const int _headBytes = 2 + 1 + 4 + 4 + 1;

        [Test]
        public void TheRoundResultRoundTrips()
        {
            var sent = Build(100);
            sent.Match.RoundNumber = 5;
            sent.Match.RoundsToWin = 4;
            sent.Match.HasLastRound = true;
            sent.Match.LastRoundEnd = RoundEndReason.Defused;
            sent.Match.HasLastRoundWinner = true;
            sent.Match.LastRoundWinner = Team.Bravo;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Match.RoundNumber, Is.EqualTo(5));
            Assert.That(got.Match.RoundsToWin, Is.EqualTo(4));
            Assert.That(got.Match.HasLastRound, Is.True);
            Assert.That(got.Match.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(got.Match.HasLastRoundWinner, Is.True);
            Assert.That(got.Match.LastRoundWinner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void ADrawnLastRoundHasNoWinner()
        {
            var sent = Build(100);
            sent.Match.HasLastRound = true;
            sent.Match.LastRoundEnd = RoundEndReason.BothEliminated;
            sent.Match.HasLastRoundWinner = false;
            sent.Match.LastRoundWinner = Team.Bravo;

            var got = Send(sent, null);

            Assert.That(got.Match.HasLastRound, Is.True);
            Assert.That(got.Match.LastRoundEnd, Is.EqualTo(RoundEndReason.BothEliminated));
            Assert.That(got.Match.HasLastRoundWinner, Is.False);
            Assert.That(got.Match.LastRoundWinner, Is.EqualTo(Team.Alpha), "a missing winner is read as the first team");
        }

        [Test]
        public void NoLastRoundIsReadAsNone()
        {
            var sent = Build(100);
            sent.Match.HasLastRound = false;
            sent.Match.LastRoundEnd = RoundEndReason.TimeUp;

            var got = Send(sent, null);

            Assert.That(got.Match.HasLastRound, Is.False);
            Assert.That(got.Match.LastRoundEnd, Is.EqualTo(default(RoundEndReason)));
        }

        [Test]
        public void TheObjectiveProgressRoundTrips()
        {
            var sent = Build(100);
            sent.Match.PlantFraction = 0.25f;
            sent.Match.HasDefuser = true;
            sent.Match.DefuserId = 7;
            sent.Match.DefuseFraction = 0.75f;

            var got = Send(sent, null);

            Assert.That(got.Match.PlantFraction, Is.EqualTo(0.25f));
            Assert.That(got.Match.HasDefuser, Is.True);
            Assert.That(got.Match.DefuserId, Is.EqualTo(7));
            Assert.That(got.Match.DefuseFraction, Is.EqualTo(0.75f));
        }

        [Test]
        public void NoDefuserIsReadAsNone()
        {
            var sent = Build(100);
            sent.Match.HasDefuser = false;
            sent.Match.DefuserId = 9;

            var got = Send(sent, null);

            Assert.That(got.Match.HasDefuser, Is.False);
            Assert.That(got.Match.DefuserId, Is.Zero);
        }

        [Test]
        public void AChangedRoundResultSendsOnlyItsBytesAndResolves()
        {
            var baseline = Build(100);
            var unchanged = Build(101);
            var current = Build(101);
            current.Match.RoundNumber = 2;
            current.Match.HasLastRound = true;
            current.Match.LastRoundEnd = RoundEndReason.Detonated;
            current.Match.HasLastRoundWinner = true;
            current.Match.LastRoundWinner = Team.Alpha;

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(_roundResultBytes));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.RoundNumber, Is.EqualTo(2));
            Assert.That(got.Match.LastRoundEnd, Is.EqualTo(RoundEndReason.Detonated));
            Assert.That(got.Match.RemainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void AChangedProgressSendsOnlyItsBytesAndKeepsTheRoundResult()
        {
            var baseline = Build(100);
            baseline.Match.RoundNumber = 3;
            baseline.Match.RoundsToWin = 4;
            var unchanged = Build(101);
            unchanged.Match.RoundNumber = 3;
            unchanged.Match.RoundsToWin = 4;
            var current = Build(101);
            current.Match.RoundNumber = 3;
            current.Match.RoundsToWin = 4;
            current.Match.PlantFraction = 0.5f;

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(_progressBytes));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.PlantFraction, Is.EqualTo(0.5f));
            Assert.That(got.Match.RoundNumber, Is.EqualTo(3), "an unchanged round result is taken from the baseline");
            Assert.That(got.Match.RoundsToWin, Is.EqualTo(4));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void NegativeRoundsAreNotWritten(int round, int toWin)
        {
            var sent = Build(100);
            sent.Match.RoundNumber = round;
            sent.Match.RoundsToWin = toWin;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AWinnerWithoutALastRoundIsNotWritten()
        {
            var sent = Build(100);
            sent.Match.HasLastRound = false;
            sent.Match.HasLastRoundWinner = true;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(-0.1f, 0f)]
        [TestCase(1.1f, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, 2f)]
        public void AFractionOutsideZeroToOneIsNotWritten(float plant, float defuse)
        {
            var sent = Build(100);
            sent.Match.PlantFraction = plant;
            sent.Match.DefuseFraction = defuse;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void ANegativeDefuserIsNotWritten()
        {
            var sent = Build(100);
            sent.Match.HasDefuser = true;
            sent.Match.DefuserId = -3;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        // 部分のバイト内の位置と、書き換える値
        [TestCase(8, 2)]
        [TestCase(9, 0xFF)]
        [TestCase(10, 2)]
        public void AnUnreadableRoundResultIsRejected(int at, int value)
        {
            var bytes = RoundResultDelta();
            bytes[_headBytes + at] = (byte)value;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AWinnerWithoutALastRoundIsUnreadable()
        {
            var bytes = RoundResultDelta();

            // 前のラウンドの有無を無しにして、勝者の有無だけを残す
            bytes[_headBytes + 8] = 0;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ANegativeRoundNumberIsUnreadable()
        {
            var bytes = RoundResultDelta();
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        // 部分のバイト内の位置 (設置の割合 0・解除の割合 8)
        [TestCase(0, 1.5f)]
        [TestCase(0, float.NaN)]
        [TestCase(8, -0.5f)]
        public void AnUnreadableFractionIsRejected(int at, float value)
        {
            var bytes = ProgressDelta();
            BitConverter.TryWriteBytes(new Span<byte>(bytes, _headBytes + at, 4), value);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ADefuserBelowNoneIsUnreadable()
        {
            var bytes = ProgressDelta();
            BitConverter.TryWriteBytes(new Span<byte>(bytes, _headBytes + 4, 4), -2);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static byte[] RoundResultDelta()
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.RoundNumber = 1;
            current.Match.RoundsToWin = 4;
            current.Match.HasLastRound = true;
            current.Match.LastRoundEnd = RoundEndReason.TimeUp;
            current.Match.HasLastRoundWinner = true;
            current.Match.LastRoundWinner = Team.Bravo;
            return Bytes(current, baseline);
        }

        private static byte[] ProgressDelta()
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.PlantFraction = 0.5f;
            current.Match.HasDefuser = true;
            current.Match.DefuserId = 3;
            current.Match.DefuseFraction = 0.5f;
            return Bytes(current, baseline);
        }

        private static SnapshotMessage Build(int tick)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.RemainingSeconds = 300;
            match.Rules = MatchRulesSync.From(GameRules.Default);
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
