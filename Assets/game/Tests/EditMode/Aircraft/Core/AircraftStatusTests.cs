using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftStatusTests
    {
        private const float _epsilon = 1e-3f;

        [Test]
        public void TheStatusCarriesTheFlightStateInDegrees()
        {
            var state = new AircraftFlightState(60f, (float)(Math.PI / 18.0), 0f, 2.5f, false);

            var status = AircraftStatus.From(state, 350f, 0.75f, AircraftWarningSpec.Default);

            Assert.That(status.Speed, Is.EqualTo(60f));
            Assert.That(status.Altitude, Is.EqualTo(350f));
            Assert.That(status.Throttle, Is.EqualTo(0.75f));
            Assert.That(status.AngleOfAttackDegrees, Is.EqualTo(10f).Within(_epsilon));
            Assert.That(status.LoadFactor, Is.EqualTo(2.5f));
            Assert.That(status.StallWarning, Is.False);
            Assert.That(status.Grounded, Is.False);
        }

        [Test]
        public void ASteepAngleOfAttackWarnsInEitherDirection()
        {
            var warning = new AircraftWarningSpec(stallAngleDegrees: 12f, stallSpeed: 0f);

            Assert.That(AircraftStatus.From(Flying(60f, 11f), 100f, 1f, warning).StallWarning, Is.False);
            Assert.That(AircraftStatus.From(Flying(60f, 12.5f), 100f, 1f, warning).StallWarning, Is.True);
            Assert.That(AircraftStatus.From(Flying(60f, -13f), 100f, 1f, warning).StallWarning, Is.True);
        }

        [Test]
        public void FlyingSlowerThanTheStallSpeedWarns()
        {
            var warning = new AircraftWarningSpec(stallAngleDegrees: 90f, stallSpeed: 30f);

            Assert.That(AircraftStatus.From(Flying(30f, 0f), 100f, 1f, warning).StallWarning, Is.False);
            Assert.That(AircraftStatus.From(Flying(29.9f, 0f), 100f, 1f, warning).StallWarning, Is.True);
        }

        [Test]
        public void NothingWarnsOnTheGround()
        {
            var parked = new AircraftFlightState(0f, (float)(Math.PI / 4.0), 0f, 0f, true);

            var status = AircraftStatus.From(parked, 2f, -1f, AircraftWarningSpec.Default);

            Assert.That(status.StallWarning, Is.False);
            Assert.That(status.Grounded, Is.True);
            Assert.That(status.Throttle, Is.EqualTo(-1f));
        }

        [Test]
        public void TheSameReadingsAreEqual()
        {
            var a = AircraftStatus.From(Flying(50f, 3f), 80f, 0.5f, AircraftWarningSpec.Default);
            var b = AircraftStatus.From(Flying(50f, 3f), 80f, 0.5f, AircraftWarningSpec.Default);
            var c = AircraftStatus.From(Flying(50f, 3f), 81f, 0.5f, AircraftWarningSpec.Default);

            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != c, Is.True);
        }

        [Test]
        public void BadValuesAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftStatus.From(Flying(50f, 0f), 0f, 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStatus.From(Flying(50f, 0f), float.NaN, 0f, AircraftWarningSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStatus.From(Flying(50f, 0f), 0f, float.PositiveInfinity, AircraftWarningSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStatus(-1f, 0f, 0f, 0f, 1f, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftWarningSpec(stallAngleDegrees: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftWarningSpec(stallAngleDegrees: 91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftWarningSpec(stallSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftWarningSpec(stallSpeed: float.NaN));
        }

        private static AircraftFlightState Flying(float airspeed, float angleOfAttackDegrees)
        {
            return new AircraftFlightState(airspeed, (float)(angleOfAttackDegrees * Math.PI / 180.0), 0f, 1f, false);
        }
    }
}
