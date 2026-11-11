using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HitboxHistoryTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HitboxHistory(capacity));
        }

        [Test]
        public void DefaultCapacityIs64()
        {
            Assert.That(new HitboxHistory().Capacity, Is.EqualTo(64));
        }

        [Test]
        public void EmptyHistoryCannotSample()
        {
            var history = new HitboxHistory();

            Assert.That(history.TrySample(0f, out _), Is.False);
        }

        [Test]
        public void ReturnsRecordedValueAtExactTick()
        {
            var history = new HitboxHistory();
            history.Record(10, new Vector3(1, 2, 3));
            history.Record(11, new Vector3(4, 5, 6));

            Assert.That(history.TrySample(10f, out var first), Is.True);
            Assert.That(first, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(history.TrySample(11f, out var last), Is.True);
            Assert.That(last, Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void InterpolatesBetweenRecords()
        {
            var history = new HitboxHistory();
            history.Record(10, new Vector3(0, 0, 0));
            history.Record(11, new Vector3(10, -4, 2));

            Assert.That(history.TrySample(10.5f, out var mid), Is.True);
            Assert.That(mid, Is.EqualTo(new Vector3(5, -2, 1)));
            Assert.That(history.TrySample(10.25f, out var quarter), Is.True);
            Assert.That(quarter, Is.EqualTo(new Vector3(2.5f, -1, 0.5f)));
        }

        [Test]
        public void InterpolatesAcrossSkippedTicks()
        {
            var history = new HitboxHistory();
            history.Record(10, new Vector3(0, 0, 0));
            history.Record(14, new Vector3(8, 0, 0));

            Assert.That(history.TrySample(11f, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(history.TrySample(13.5f, out position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(7, 0, 0)));
        }

        [TestCase(9.99f)]
        [TestCase(12.01f)]
        [TestCase(float.NaN)]
        public void OutOfRangeCannotSample(float tick)
        {
            var history = new HitboxHistory();
            history.Record(10, Vector3.Zero);
            history.Record(12, Vector3.One);

            Assert.That(history.TrySample(tick, out _), Is.False);
        }

        [Test]
        public void OverwritesOldestWhenFull()
        {
            var history = new HitboxHistory(3);
            for (var tick = 0; tick < 5; tick++)
            {
                history.Record(tick, new Vector3(tick, 0, 0));
            }

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.TrySample(1f, out _), Is.False);
            Assert.That(history.TrySample(1.5f, out _), Is.False);
            Assert.That(history.TrySample(2f, out var oldest), Is.True);
            Assert.That(oldest, Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(history.TrySample(3.5f, out var mid), Is.True);
            Assert.That(mid, Is.EqualTo(new Vector3(3.5f, 0, 0)));
            Assert.That(history.TrySample(4f, out var newest), Is.True);
            Assert.That(newest, Is.EqualTo(new Vector3(4, 0, 0)));
        }

        [Test]
        public void InterpolatesAcrossRingBufferWrap()
        {
            var history = new HitboxHistory(4);
            for (var tick = 0; tick < 10; tick++)
            {
                history.Record(tick * 2, new Vector3(tick * 2, 0, 0));
            }

            for (var tick = 12f; tick <= 18f; tick += 0.5f)
            {
                Assert.That(history.TrySample(tick, out var position), Is.True);
                Assert.That(position.X, Is.EqualTo(tick).Within(1e-5f));
            }

            Assert.That(history.TrySample(11.5f, out _), Is.False);
        }

        [Test]
        public void SameTickOverwritesNewest()
        {
            var history = new HitboxHistory(2);
            history.Record(1, Vector3.Zero);
            history.Record(2, Vector3.One);
            history.Record(2, new Vector3(5, 5, 5));

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.TrySample(1f, out _), Is.True);
            Assert.That(history.TrySample(2f, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(5, 5, 5)));
        }

        [Test]
        public void RejectsOlderTick()
        {
            var history = new HitboxHistory();
            history.Record(5, Vector3.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => history.Record(4, Vector3.One));
            Assert.That(history.Count, Is.EqualTo(1));
        }
    }
}
