using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StreakAimTests
    {
        private const float _tolerance = 1e-4f;

        private static readonly Vector3 _origin = new Vector3(10f, 2f, 20f);

        [Test]
        public void TheAimStartsUnderTheUserFacingTheGivenWay()
        {
            var aim = Aim(heading: 370f);

            Assert.That(aim.Target, Is.EqualTo(_origin));
            Assert.That(aim.HeadingDegrees, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(aim.RewardId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(aim.DisplayNameKey, Is.EqualTo(StreakSpec.DefaultAirstrikeNameKey));
            Assert.That(aim.Range, Is.EqualTo(50f));
            Assert.That(aim.LineLength, Is.EqualTo(24f));
        }

        [Test]
        public void MovingSlidesTheTargetEastAndNorthAtTheSpeed()
        {
            var aim = Aim();

            aim.Move(new Vector2(1f, 0f), 40f, 0.25f);
            AssertClose(aim.Target, _origin + new Vector3(10f, 0f, 0f));

            aim.Move(new Vector2(0f, -0.5f), 40f, 0.5f);
            AssertClose(aim.Target, _origin + new Vector3(10f, 0f, -10f));
        }

        [Test]
        public void ADiagonalIsNoFasterThanAStraightMove()
        {
            var aim = Aim();

            aim.Move(new Vector2(1f, 1f), 10f, 1f);

            var moved = new Vector2(aim.Target.X - _origin.X, aim.Target.Z - _origin.Z);
            Assert.That(moved.Length(), Is.EqualTo(10f).Within(_tolerance));
        }

        [Test]
        public void TheTargetStaysInsideTheRangeCircle()
        {
            var aim = Aim();

            aim.Move(new Vector2(1f, 0f), 100f, 1f);
            AssertClose(aim.Target, _origin + new Vector3(50f, 0f, 0f));

            aim.Place(new Vector3(_origin.X - 60f, 7f, _origin.Z + 80f));
            AssertClose(aim.Target, new Vector3(_origin.X - 30f, 7f, _origin.Z + 40f));
            Assert.That(AirstrikeLine.IsWithinRange(_origin, aim.Target, aim.Range + _tolerance), Is.True);
        }

        [Test]
        public void TurningWrapsTheHeading()
        {
            var aim = Aim(heading: 350f);

            aim.Turn(20f);
            Assert.That(aim.HeadingDegrees, Is.EqualTo(10f).Within(_tolerance));

            aim.Turn(-30f);
            Assert.That(aim.HeadingDegrees, Is.EqualTo(340f).Within(_tolerance));
        }

        [Test]
        public void TheGroundHeightReplacesOnlyTheHeight()
        {
            var aim = Aim();
            aim.Move(new Vector2(0f, 1f), 10f, 1f);

            aim.SetGroundHeight(-3.5f);

            AssertClose(aim.Target, new Vector3(_origin.X, -3.5f, _origin.Z + 10f));
        }

        [Test]
        public void TheStatusCarriesTheCurrentAim()
        {
            var aim = Aim(heading: 90f);
            aim.Move(new Vector2(-1f, 0f), 5f, 1f);

            var status = aim.Status;

            Assert.That(status.RewardId, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(status.DisplayNameKey, Is.EqualTo(StreakSpec.DefaultAirstrikeNameKey));
            Assert.That(status.Origin, Is.EqualTo(_origin));
            Assert.That(status.Target, Is.EqualTo(aim.Target));
            Assert.That(status.HeadingDegrees, Is.EqualTo(90f));
            Assert.That(status.Range, Is.EqualTo(50f));
            Assert.That(status.LineLength, Is.EqualTo(24f));
            Assert.That(status, Is.EqualTo(aim.Status), "the same aim gives an equal status");

            aim.Turn(1f);
            Assert.That(status, Is.Not.EqualTo(aim.Status));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => new StreakAim(" ", null, _origin, 0f, 50f, 24f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakAim("airstrike", null, new Vector3(float.NaN, 0f, 0f), 0f, 50f, 24f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakAim("airstrike", null, _origin, float.NaN, 50f, 24f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakAim("airstrike", null, _origin, 0f, 0f, 24f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakAim("airstrike", null, _origin, 0f, 50f, -1f));
            Assert.That(new StreakAim("airstrike", null, _origin, 0f, 50f, 24f).DisplayNameKey, Is.Empty);

            var aim = Aim();
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Move(new Vector2(float.NaN, 0f), 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Move(Vector2.UnitX, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Move(Vector2.UnitX, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Turn(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.SetGroundHeight(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Place(new Vector3(0f, float.NaN, 0f)));
            Assert.That(aim.Target, Is.EqualTo(_origin), "a refused input leaves the aim where it was");
        }

        private static StreakAim Aim(float heading = 0f)
        {
            return new StreakAim(StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, _origin, heading, 50f, 24f);
        }

        private static void AssertClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(_tolerance), actual + " should be " + expected);
        }
    }
}
