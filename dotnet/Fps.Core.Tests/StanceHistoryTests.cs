using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class StanceHistoryTests
    {
        [Test]
        public void RejectsNonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StanceHistory(0));
            Assert.That(new StanceHistory().Capacity, Is.EqualTo(HitboxHistory.DefaultCapacity));
        }

        [Test]
        public void SamplesTheNewestRecordAtOrBeforeTheTick()
        {
            var history = new StanceHistory();
            history.Record(10, false);
            history.Record(12, true);
            history.Record(15, false);

            Assert.That(history.TrySample(10f, out var at10), Is.True);
            Assert.That(at10, Is.False);
            Assert.That(history.TrySample(11.9f, out var at11), Is.True);
            Assert.That(at11, Is.False);
            Assert.That(history.TrySample(12f, out var at12), Is.True);
            Assert.That(at12, Is.True);
            Assert.That(history.TrySample(14.5f, out var at14), Is.True);
            Assert.That(at14, Is.True);
            Assert.That(history.TrySample(15f, out var at15), Is.True);
            Assert.That(at15, Is.False);
        }

        [Test]
        public void OutOfRangeOrNaNIsNotSampled()
        {
            var history = new StanceHistory();
            Assert.That(history.TrySample(0f, out _), Is.False);

            history.Record(10, true);
            history.Record(11, true);

            Assert.That(history.TrySample(9.9f, out _), Is.False);
            Assert.That(history.TrySample(11.1f, out _), Is.False);
            Assert.That(history.TrySample(float.NaN, out _), Is.False);
        }

        [Test]
        public void SameTickOverwritesAndOlderTickThrows()
        {
            var history = new StanceHistory();
            history.Record(10, false);
            history.Record(10, true);

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history.TrySample(10f, out var crouched), Is.True);
            Assert.That(crouched, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => history.Record(9, false));
        }

        [Test]
        public void OverwritesTheOldestWhenFull()
        {
            var history = new StanceHistory(3);
            for (var tick = 1; tick <= 5; tick++)
            {
                history.Record(tick, tick % 2 == 0);
            }

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.TrySample(2f, out _), Is.False);
            Assert.That(history.TrySample(3f, out var at3), Is.True);
            Assert.That(at3, Is.False);
            Assert.That(history.TrySample(4.5f, out var at4), Is.True);
            Assert.That(at4, Is.True);
        }
    }
}
