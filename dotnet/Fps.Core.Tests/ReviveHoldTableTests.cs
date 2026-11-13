using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ReviveHoldTableTests
    {
        private const double _grace = ReviveHoldTable.DefaultGraceSeconds;

        [Test]
        public void HoldIsActiveWithinTheGrace()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(0, 2, 10d);

            var active = holds.TakeActive(10d + _grace, _grace);

            Assert.That(active, Is.EqualTo(new[] { new ReviveHold(0, 2) }));
            Assert.That(holds.TryGetTarget(0, out var target), Is.True);
            Assert.That(target, Is.EqualTo(2));
        }

        [Test]
        public void HoldExpiresAfterTheGraceAndIsForgotten()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(0, 2, 10d);

            Assert.That(holds.TakeActive(10d + _grace + 0.01d, _grace), Is.Empty);
            Assert.That(holds.Count, Is.Zero);
            Assert.That(holds.TryGetTarget(0, out _), Is.False);
        }

        [Test]
        public void HoldingAgainKeepsItActiveAndCanSwitchTheTarget()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(0, 2, 10d);
            holds.Hold(0, 4, 10.5d);

            Assert.That(holds.TakeActive(10.6d, _grace), Is.EqualTo(new[] { new ReviveHold(0, 4) }));
        }

        [Test]
        public void ActiveHoldsAreInReviverOrder()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(5, 1, 0d);
            holds.Hold(3, 1, 0d);
            holds.Hold(4, 2, 0d);

            Assert.That(
                holds.TakeActive(0d, _grace),
                Is.EqualTo(new[] { new ReviveHold(3, 1), new ReviveHold(4, 2), new ReviveHold(5, 1) }));
        }

        [Test]
        public void RemovePlayerDropsItAsReviverAndAsTarget()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(0, 2, 0d);
            holds.Hold(4, 0, 0d);
            holds.Hold(6, 8, 0d);

            holds.RemovePlayer(0);

            Assert.That(holds.TakeActive(0d, _grace), Is.EqualTo(new[] { new ReviveHold(6, 8) }));
        }

        [Test]
        public void ReleaseAndClearForgetHolds()
        {
            var holds = new ReviveHoldTable();
            holds.Hold(0, 2, 0d);
            holds.Hold(4, 6, 0d);

            Assert.That(holds.Release(0), Is.True);
            Assert.That(holds.Release(0), Is.False);
            Assert.That(holds.Count, Is.EqualTo(1));

            holds.Clear();
            Assert.That(holds.Count, Is.Zero);
        }

        [Test]
        public void RejectsTimesThatAreNotFinite()
        {
            var holds = new ReviveHoldTable();

            Assert.Throws<ArgumentOutOfRangeException>(() => holds.Hold(0, 2, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => holds.TakeActive(double.PositiveInfinity, _grace));
            Assert.Throws<ArgumentOutOfRangeException>(() => holds.TakeActive(0d, -1d));
        }
    }
}
