using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KnockbackFlightTests
    {
        private const float Gravity = 20f;

        [Test]
        public void StartsGrounded()
        {
            var flight = new KnockbackFlight();

            Assert.That(flight.IsFlying, Is.False);
            Assert.That(flight.Step(0.1f, Gravity), Is.EqualTo(Vector3.Zero));
            Assert.That(flight.CanLand(0f), Is.False);
            Assert.That(flight.HasTimedOut(0.01f), Is.False);
        }

        [Test]
        public void RisesFallsAndCanLandOnlyAfterTheMinimumAndWhileFalling()
        {
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(2f, 4f, 0f));

            var moved = flight.Step(0.1f, Gravity);
            Assert.That(moved.X, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(moved.Y, Is.EqualTo(0.3f).Within(1e-5f), "trapezoid of 4 and 2 m/s over 0.1s");
            Assert.That(flight.Velocity.Y, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(flight.CanLand(0.05f), Is.False, "still rising");

            flight.Step(0.15f, Gravity);
            Assert.That(flight.Velocity.Y, Is.LessThan(0f));
            Assert.That(flight.CanLand(0.3f), Is.False, "not in the air long enough");
            Assert.That(flight.CanLand(0.2f), Is.True);

            Assert.That(flight.Land(), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(flight.IsFlying, Is.False);
            Assert.That(flight.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ReachesTheSameHeightWhateverTheStep()
        {
            Assert.That(PeakHeight(0.005f), Is.EqualTo(PeakHeight(0.05f)).Within(0.05f));
        }

        [Test]
        public void ASecondPushAddsToTheFirst()
        {
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(3f, 2f, 0f));
            flight.Step(0.1f, Gravity);

            flight.Launch(new Vector3(0f, 0f, 4f));

            Assert.That(flight.Velocity.X, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(flight.Velocity.Z, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(flight.ElapsedSeconds, Is.EqualTo(0.1f).Within(1e-5f), "the clock keeps running");
        }

        [Test]
        public void SteeringPullsTheHorizontalSpeedSlowly()
        {
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(10f, 0f, 0f));

            flight.Step(0.1f, Gravity, new Vector2(0f, 5f), 1f);

            Assert.That(flight.Velocity.X, Is.LessThan(10f));
            Assert.That(flight.Velocity.X, Is.GreaterThan(9f), "air control is weak");
            Assert.That(flight.Velocity.Z, Is.GreaterThan(0f));
        }

        [Test]
        public void StopsAgainstWallsAndCeilings()
        {
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(5f, 5f, 5f));

            flight.StopHorizontal();
            Assert.That(flight.Velocity, Is.EqualTo(new Vector3(0f, 5f, 0f)));

            flight.StopRising();
            Assert.That(flight.Velocity, Is.EqualTo(Vector3.Zero));

            flight.Launch(new Vector3(0f, -3f, 0f));
            flight.StopRising();
            Assert.That(flight.Velocity.Y, Is.EqualTo(-3f), "falling is not stopped by a ceiling");
        }

        [Test]
        public void TimesOut()
        {
            var flight = new KnockbackFlight();
            flight.Launch(Vector3.UnitY);

            flight.Step(1f, Gravity);
            Assert.That(flight.HasTimedOut(2f), Is.False);
            flight.Step(1f, Gravity);
            Assert.That(flight.HasTimedOut(2f), Is.True);
        }

        [Test]
        public void ClearStopsAtOnce()
        {
            var flight = new KnockbackFlight();
            flight.Launch(Vector3.UnitX);

            flight.Clear();

            Assert.That(flight.IsFlying, Is.False);
            Assert.That(flight.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var flight = new KnockbackFlight();

            Assert.Throws<ArgumentException>(() => flight.Launch(new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(-0.1f, Gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(0.1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(0.1f, Gravity, Vector2.Zero, float.NaN));
            Assert.Throws<ArgumentException>(() => flight.Step(0.1f, Gravity, new Vector2(float.PositiveInfinity, 0f), 1f));
            Assert.That(flight.IsFlying, Is.False);
        }

        private static float PeakHeight(float step)
        {
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(0f, 6f, 0f));
            var height = 0f;
            var peak = 0f;
            while (flight.Velocity.Y > -6f)
            {
                height += flight.Step(step, Gravity).Y;
                peak = Math.Max(peak, height);
            }

            return peak;
        }
    }
}
