using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VehiclePoseHistoryTests
    {
        private static readonly Quaternion _quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

        [Test]
        public void ATickBetweenTwoRecordsIsInterpolated()
        {
            var history = new VehiclePoseHistory();
            history.Record(10, Vector3.Zero, Quaternion.Identity);
            history.Record(12, new Vector3(4f, 0f, 0f), _quarter);

            Assert.That(history.TrySample(11.5f, out var position, out var rotation), Is.True);
            Assert.That(position.X, Is.EqualTo(3f).Within(1e-5f));
            var expected = Quaternion.Slerp(Quaternion.Identity, _quarter, 0.75f);
            Assert.That(Quaternion.Dot(rotation, expected), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TicksOutsideTheRecordsUseTheNearestEnd()
        {
            var history = new VehiclePoseHistory();
            history.Record(10, Vector3.Zero, Quaternion.Identity);
            history.Record(11, Vector3.UnitX, Quaternion.Identity);

            history.TrySample(3f, out var older, out _);
            history.TrySample(30f, out var newer, out _);

            Assert.That(older, Is.EqualTo(Vector3.Zero));
            Assert.That(newer, Is.EqualTo(Vector3.UnitX));
        }

        [Test]
        public void TheOldestRecordIsDroppedWhenFull()
        {
            var history = new VehiclePoseHistory(2);
            history.Record(1, Vector3.Zero, Quaternion.Identity);
            history.Record(2, Vector3.UnitX, Quaternion.Identity);
            history.Record(3, Vector3.UnitY, Quaternion.Identity);

            history.TrySample(0f, out var oldest, out _);

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(oldest, Is.EqualTo(Vector3.UnitX));
            Assert.That(history.NewestTick, Is.EqualTo(3));
        }

        [Test]
        public void TheSameTickOverwritesAndAnOlderTickIsRefused()
        {
            var history = new VehiclePoseHistory();
            history.Record(5, Vector3.Zero, Quaternion.Identity);
            history.Record(5, Vector3.UnitZ, Quaternion.Identity);

            history.TrySample(5f, out var position, out _);

            Assert.That(position, Is.EqualTo(Vector3.UnitZ));
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(() => history.Record(4, Vector3.Zero, Quaternion.Identity), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AnEmptyHistoryHasNoPose()
        {
            var history = new VehiclePoseHistory();

            Assert.That(history.TrySample(1f, out _, out _), Is.False);
            history.Record(1, Vector3.One, Quaternion.Identity);
            history.Clear();
            Assert.That(history.TrySample(1f, out _, out _), Is.False);
        }
    }
}
