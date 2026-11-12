using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class LagCompensatorTests
    {
        [Test]
        public void DefaultsTo64TicksOfHistoryAnd12TicksOfRewind()
        {
            var compensator = new LagCompensator();

            Assert.That(compensator.HistoryCapacity, Is.EqualTo(64));
            Assert.That(compensator.MaxRewindTicks, Is.EqualTo(12));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LagCompensator(historyCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LagCompensator(maxRewindTicks: -1));
        }

        [Test]
        public void RewindReturnsOnlyEntitiesWithPositionAtTick()
        {
            var compensator = new LagCompensator();
            compensator.Record(10, 1, new Vector3(0, 0, 0));
            compensator.Record(11, 1, new Vector3(2, 0, 0));
            compensator.Record(11, 2, new Vector3(9, 9, 9));

            var positions = compensator.Rewind(10.5f);

            Assert.That(positions, Has.Count.EqualTo(1));
            Assert.That(positions[1], Is.EqualTo(new Vector3(1, 0, 0)));
        }

        [Test]
        public void RewindInterpolatesAcrossSkippedTicks()
        {
            var compensator = new LagCompensator();
            compensator.Record(0, 7, Vector3.Zero);
            compensator.Record(3, 7, new Vector3(0, 0, 6));

            var positions = compensator.Rewind(2f);

            Assert.That(positions[7], Is.EqualTo(new Vector3(0, 0, 4)));
        }

        [Test]
        public void RemoveDropsEntity()
        {
            var compensator = new LagCompensator();
            compensator.Record(1, 1, Vector3.Zero);
            compensator.Record(1, 2, Vector3.One);

            Assert.That(compensator.Remove(1), Is.True);
            Assert.That(compensator.Remove(1), Is.False);

            var positions = compensator.Rewind(1f);
            Assert.That(positions.ContainsKey(1), Is.False);
            Assert.That(positions[2], Is.EqualTo(Vector3.One));
        }

        [Test]
        public void RemovedEntityStartsFreshHistory()
        {
            var compensator = new LagCompensator();
            compensator.Record(5, 1, Vector3.Zero);
            compensator.Remove(1);

            compensator.Record(2, 1, Vector3.One);

            Assert.That(compensator.Rewind(5f).ContainsKey(1), Is.False);
            Assert.That(compensator.Rewind(2f)[1], Is.EqualTo(Vector3.One));
        }

        [Test]
        public void RewindClampsToMaxRewindTicks()
        {
            var compensator = new LagCompensator(maxRewindTicks: 12);
            for (var tick = 0; tick <= 30; tick++)
            {
                compensator.Record(tick, 1, new Vector3(tick, 0, 0));
            }

            Assert.That(compensator.Rewind(5f, 30)[1], Is.EqualTo(new Vector3(18, 0, 0)));
            Assert.That(compensator.Rewind(18f, 30)[1], Is.EqualTo(new Vector3(18, 0, 0)));
            Assert.That(compensator.Rewind(20.5f, 30)[1], Is.EqualTo(new Vector3(20.5f, 0, 0)));
        }

        [Test]
        public void RewindClampsFutureTickToCurrentTick()
        {
            var compensator = new LagCompensator();
            compensator.Record(10, 1, Vector3.One);

            Assert.That(compensator.Rewind(11f, 10)[1], Is.EqualTo(Vector3.One));
        }

        [Test]
        public void ClampTick()
        {
            var compensator = new LagCompensator(maxRewindTicks: 4);

            Assert.That(compensator.ClampTick(1f, 10), Is.EqualTo(6f));
            Assert.That(compensator.ClampTick(7.5f, 10), Is.EqualTo(7.5f));
            Assert.That(compensator.ClampTick(12f, 10), Is.EqualTo(10f));
            Assert.That(compensator.ClampTick(float.NaN, 10), Is.NaN);
        }
    }
}
