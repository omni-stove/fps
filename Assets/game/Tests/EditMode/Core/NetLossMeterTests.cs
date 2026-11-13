using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetLossMeterTests
    {
        [Test]
        public void IsUnknownUntilAProbeTimesOut()
        {
            var meter = new NetLossMeter(1.0);
            meter.Send(0.0);

            Assert.That(meter.TakeLossPercent(0.5), Is.Null);
            Assert.That(meter.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void AnsweredProbesAreNotLost()
        {
            var meter = new NetLossMeter(1.0);
            for (var i = 0; i < 10; i++)
            {
                var sequence = meter.Send(i * 0.1);
                Assert.That(meter.Acknowledge(sequence, (i * 0.1) + 0.05), Is.True);
            }

            Assert.That(meter.TakeLossPercent(2.0), Is.EqualTo(0));
            Assert.That(meter.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void UnansweredProbesAreCountedAsLost()
        {
            var meter = new NetLossMeter(1.0);
            for (var i = 0; i < 20; i++)
            {
                var sequence = meter.Send(i * 0.05);
                if (i % 4 != 0)
                {
                    meter.Acknowledge(sequence, (i * 0.05) + 0.2);
                }
            }

            Assert.That(meter.TakeLossPercent(5.0), Is.EqualTo(25));
        }

        [Test]
        public void TakingTheResultStartsANewCount()
        {
            var meter = new NetLossMeter(1.0);
            meter.Send(0.0);

            Assert.That(meter.TakeLossPercent(1.0), Is.EqualTo(100));
            Assert.That(meter.TakeLossPercent(1.5), Is.Null);
            Assert.That(meter.SettledCount, Is.EqualTo(0));
        }

        [Test]
        public void OnlyProbesOlderThanTheTimeoutAreSettled()
        {
            var meter = new NetLossMeter(1.0);
            meter.Send(0.0);
            var young = meter.Send(0.8);

            Assert.That(meter.TakeLossPercent(1.0), Is.EqualTo(100), "only the first probe is settled");
            Assert.That(meter.Acknowledge(young, 1.2), Is.True);
            Assert.That(meter.TakeLossPercent(2.0), Is.EqualTo(0));
        }

        [Test]
        public void LateDuplicateAndUnknownAnswersAreIgnored()
        {
            var meter = new NetLossMeter(1.0);
            var late = meter.Send(0.0);
            var twice = meter.Send(0.0);

            Assert.That(meter.Acknowledge(late, 1.5), Is.False, "too late");
            Assert.That(meter.Acknowledge(twice, 0.1), Is.True);
            Assert.That(meter.Acknowledge(twice, 0.2), Is.False, "a duplicate");
            Assert.That(meter.Acknowledge(999, 0.2), Is.False, "never sent");

            Assert.That(meter.TakeLossPercent(2.0), Is.EqualTo(50));
            Assert.That(meter.Acknowledge(twice, 2.1), Is.False, "already settled");
        }

        [Test]
        public void RoundsHalfAwayFromZero()
        {
            var meter = new NetLossMeter(1.0);
            for (var i = 0; i < 8; i++)
            {
                var sequence = meter.Send(0.0);
                if (i != 0)
                {
                    meter.Acknowledge(sequence, 0.1);
                }
            }

            // 1/8 = 12.5%
            Assert.That(meter.TakeLossPercent(1.0), Is.EqualTo(13));
        }

        [Test]
        public void OverflowClosesTheOldestProbeAsLost()
        {
            var meter = new NetLossMeter(1.0);
            var first = meter.Send(0.0);
            for (var i = 1; i < NetLossMeter.Capacity; i++)
            {
                meter.Acknowledge(meter.Send(0.0), 0.0);
            }

            meter.Acknowledge(meter.Send(0.0), 0.0);

            Assert.That(meter.PendingCount, Is.EqualTo(NetLossMeter.Capacity));
            Assert.That(meter.LostCount, Is.EqualTo(1));
            Assert.That(meter.Acknowledge(first, 0.0), Is.False, "its slot now holds a newer probe");
        }

        [Test]
        public void SequenceNumbersWrapAround()
        {
            var meter = new NetLossMeter(1.0);
            ushort last = 0;
            for (var i = 0; i <= ushort.MaxValue + 1; i++)
            {
                last = meter.Send(i);
                meter.Acknowledge(last, i);
                meter.TakeLossPercent(i);
            }

            Assert.That(last, Is.EqualTo(0));
            Assert.That(meter.Acknowledge(last, ushort.MaxValue + 1), Is.False, "already answered");
        }

        [Test]
        public void ResetForgetsPendingProbes()
        {
            var meter = new NetLossMeter(1.0);
            var sequence = meter.Send(0.0);
            meter.Send(0.0);

            meter.Reset();

            Assert.That(meter.PendingCount, Is.EqualTo(0));
            Assert.That(meter.Acknowledge(sequence, 0.1), Is.False);
            Assert.That(meter.TakeLossPercent(5.0), Is.Null);
            Assert.That(meter.Send(5.0), Is.EqualTo(2), "numbers continue so old answers do not match new probes");
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsAnInvalidTimeout(double timeout)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetLossMeter(timeout));
        }
    }
}
