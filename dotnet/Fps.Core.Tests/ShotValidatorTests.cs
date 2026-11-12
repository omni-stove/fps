using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ShotValidatorTests
    {
        private static readonly ShotTime _valid = new ShotTime(100, 0.5f);

        [Test]
        public void AcceptsShotNearTheShooter()
        {
            var result = ShotValidator.Check(new Vector3(0, 0.6f, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void AcceptsOriginExactlyAtTheLimit()
        {
            var result = ShotValidator.Check(new Vector3(3f, 0, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RejectsOriginFarFromTheShooter()
        {
            var result = ShotValidator.Check(new Vector3(0, 0, 3.01f), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.OriginTooFar));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsNonFiniteOrigin(float value)
        {
            var result = ShotValidator.Check(new Vector3(value, 0, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.InvalidOrigin));
        }

        [Test]
        public void RejectsZeroOrNonFiniteDirection()
        {
            Assert.That(ShotValidator.Check(Vector3.Zero, Vector3.Zero, _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.InvalidDirection));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, new Vector3(float.NaN, 0, 1), _valid, Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidDirection));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, new Vector3(0, float.NegativeInfinity, 0), _valid, Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidDirection));
        }

        [Test]
        public void AcceptsDirectionOfAnyNonZeroLength()
        {
            Assert.That(ShotValidator.Check(Vector3.Zero, new Vector3(0, 0, 50), _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.None));
            Assert.That(ShotValidator.Check(Vector3.Zero, new Vector3(0, 0, 0.01f), _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RejectsInvalidTime()
        {
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(-1, 0f), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(5, 1.5f), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(5, float.NaN), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
        }

        [Test]
        public void RejectsInvalidLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, float.PositiveInfinity));
        }
    }
}
