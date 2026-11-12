using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AntiCheatPeersTests
    {
        private const double _interval = 1;
        private const double _timeout = 5;

        private static readonly ulong[] _onlyPeer1 = { 1UL };
        private static readonly byte[] _oneByte = { 1 };

        private static AntiCheatPeers Create(bool enabled)
        {
            return new AntiCheatPeers(enabled, _interval, _timeout);
        }

        [Test]
        public void BothEnabledRegistersWithDistinctHandles()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            peers.Connected(2, 0);

            var first = peers.ReceiveHello(1, true, out var handle1, out var reason1);
            var second = peers.ReceiveHello(2, true, out var handle2, out _);

            Assert.That(first, Is.EqualTo(AntiCheatHelloVerdict.Registered));
            Assert.That(second, Is.EqualTo(AntiCheatHelloVerdict.Registered));
            Assert.That(reason1, Is.Null);
            Assert.That(handle1, Is.Not.Zero);
            Assert.That(handle2, Is.Not.EqualTo(handle1));
            Assert.That(peers.TryGetHandle(1, out var found), Is.True);
            Assert.That(found, Is.EqualTo(handle1));
            Assert.That(peers.TryGetPeer(handle2, out var peer), Is.True);
            Assert.That(peer, Is.EqualTo(2UL));
        }

        [Test]
        public void BothDisabledAcceptsWithoutHandle()
        {
            var peers = Create(false);
            peers.Connected(1, 0);

            var verdict = peers.ReceiveHello(1, false, out var handle, out var reason);

            Assert.That(verdict, Is.EqualTo(AntiCheatHelloVerdict.Accepted));
            Assert.That(handle, Is.Zero);
            Assert.That(reason, Is.Null);
            Assert.That(peers.TryGetHandle(1, out _), Is.False);
        }

        [Test]
        public void MismatchIsRejectedOnBothSides()
        {
            var protectedSide = Create(true);
            var unprotectedSide = Create(false);
            protectedSide.Connected(1, 0);
            unprotectedSide.Connected(0, 0);

            var fromProtected = protectedSide.ReceiveHello(1, false, out var handle, out var protectedReason);
            var fromUnprotected = unprotectedSide.ReceiveHello(0, true, out _, out var unprotectedReason);

            Assert.That(fromProtected, Is.EqualTo(AntiCheatHelloVerdict.Rejected));
            Assert.That(fromUnprotected, Is.EqualTo(AntiCheatHelloVerdict.Rejected));
            Assert.That(handle, Is.Zero);
            Assert.That(protectedReason, Does.Contain("-noeac"));
            Assert.That(unprotectedReason, Does.Contain("requires anti-cheat"));
        }

        [Test]
        public void SecondHelloIsDuplicate()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            peers.ReceiveHello(1, true, out var handle, out _);

            var again = peers.ReceiveHello(1, true, out var againHandle, out _);

            Assert.That(again, Is.EqualTo(AntiCheatHelloVerdict.Duplicate));
            Assert.That(againHandle, Is.Zero);
            Assert.That(peers.TryGetHandle(1, out var kept), Is.True);
            Assert.That(kept, Is.EqualTo(handle));
        }

        [Test]
        public void HelloFromUnknownPeerIsIgnored()
        {
            var peers = Create(true);

            Assert.That(peers.ReceiveHello(7, true, out _, out _), Is.EqualTo(AntiCheatHelloVerdict.Unknown));
        }

        [Test]
        public void ConnectedTwiceKeepsTheFirst()
        {
            var peers = Create(true);

            Assert.That(peers.Connected(1, 0), Is.True);
            Assert.That(peers.Connected(1, 3), Is.False);
            Assert.That(peers.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectReturnsHandleToUnregisterAndNeverReusesIt()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            peers.ReceiveHello(1, true, out var handle, out _);

            Assert.That(peers.Disconnected(1, out var released), Is.True);
            Assert.That(released, Is.EqualTo(handle));
            Assert.That(peers.TryGetPeer(handle, out _), Is.False);
            Assert.That(peers.Disconnected(1, out _), Is.False);

            peers.Connected(1, 10);
            peers.ReceiveHello(1, true, out var again, out _);
            Assert.That(again, Is.Not.EqualTo(handle));
        }

        [Test]
        public void DisconnectBeforeHelloHasNoHandle()
        {
            var peers = Create(true);
            peers.Connected(1, 0);

            Assert.That(peers.Disconnected(1, out var handle), Is.True);
            Assert.That(handle, Is.Zero);
        }

        [Test]
        public void ResendsHelloEveryIntervalUntilAnswered()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            var due = new List<ulong>();
            var timedOut = new List<ulong>();

            peers.CollectDue(0.5, due, timedOut);
            Assert.That(due, Is.Empty);

            peers.CollectDue(1.0, due, timedOut);
            Assert.That(due, Is.EqualTo(_onlyPeer1));

            due.Clear();
            peers.CollectDue(1.5, due, timedOut);
            Assert.That(due, Is.Empty);

            peers.ReceiveHello(1, true, out _, out _);
            peers.CollectDue(10, due, timedOut);
            Assert.That(due, Is.Empty);
            Assert.That(timedOut, Is.Empty);
        }

        [Test]
        public void TimesOutOnceWithoutHello()
        {
            var peers = Create(false);
            peers.Connected(1, 0);
            var due = new List<ulong>();
            var timedOut = new List<ulong>();

            peers.CollectDue(_timeout, due, timedOut);
            Assert.That(timedOut, Is.EqualTo(_onlyPeer1));
            Assert.That(due, Is.Empty);

            timedOut.Clear();
            peers.CollectDue(_timeout + 10, due, timedOut);
            Assert.That(timedOut, Is.Empty);
            Assert.That(due, Is.Empty);
            Assert.That(peers.TimeoutReason(), Does.Contain("5s"));
        }

        [Test]
        public void QueuesEarlyMessagesUntilRegistered()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            var source = new byte[] { 1, 2, 3 };

            Assert.That(peers.IsAwaitingHello(1), Is.True);
            Assert.That(peers.TryQueueEarlyMessage(1, new ArraySegment<byte>(source, 1, 2)), Is.True);
            source[1] = 9;

            peers.ReceiveHello(1, true, out _, out _);
            var early = peers.TakeEarlyMessages(1);

            Assert.That(peers.IsAwaitingHello(1), Is.False);
            Assert.That(early, Has.Count.EqualTo(1));
            var expected = new byte[] { 2, 3 };
            Assert.That(early[0], Is.EqualTo(expected));
            Assert.That(peers.TakeEarlyMessages(1), Is.Empty);
            Assert.That(peers.TryQueueEarlyMessage(1, new ArraySegment<byte>(source)), Is.False);
        }

        [Test]
        public void EarlyMessagesAreBounded()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            var payload = new ArraySegment<byte>(_oneByte);

            for (var i = 0; i < AntiCheatPeers.MaxEarlyMessagesPerPeer; i++)
            {
                Assert.That(peers.TryQueueEarlyMessage(1, payload), Is.True);
            }

            Assert.That(peers.TryQueueEarlyMessage(1, payload), Is.False);
        }

        [Test]
        public void EarlyMessagesAreNotKeptWhenDisabledOrUnknown()
        {
            var disabled = Create(false);
            disabled.Connected(1, 0);
            var payload = new ArraySegment<byte>(_oneByte);

            Assert.That(disabled.TryQueueEarlyMessage(1, payload), Is.False);
            Assert.That(Create(true).TryQueueEarlyMessage(1, payload), Is.False);
        }

        [Test]
        public void ClearForgetsPeersButKeepsCountingHandles()
        {
            var peers = Create(true);
            peers.Connected(1, 0);
            peers.ReceiveHello(1, true, out var first, out _);

            peers.Clear();
            peers.Connected(1, 0);
            peers.ReceiveHello(1, true, out var second, out _);

            Assert.That(peers.Count, Is.EqualTo(1));
            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void RejectsInvalidTimings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AntiCheatPeers(true, 0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AntiCheatPeers(true, double.NaN, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AntiCheatPeers(true, 2, 1));
        }
    }
}
