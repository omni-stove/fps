using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class BallisticFlightTests
    {
        private const int _everything = ~0;
        private const float _tolerance = 1e-3f;

        private static readonly Vector3 _gravity = new Vector3(0f, -10f, 0f);

        [TestCase(1f, 0.05f)]
        [TestCase(0.5f, 0.02f)]
        [TestCase(2f, 0.1f)]
        public void FollowsTheParabolaOfTheSpeedAndScaledGravityWhateverTheStep(float gravityMultiplier, float step)
        {
            var flight = Flight(new ProjectileSpec(20f, gravityMultiplier, 10f), new FakeWorldCollision(), Vector3.UnitZ);

            for (var i = 1; i <= 20; i++)
            {
                flight.Step(step);
                var t = step * i;
                var expected = new Vector3(0f, 0.5f * _gravity.Y * gravityMultiplier * t * t, 20f * t);
                Assert.That(Vector3.Distance(flight.Position, expected), Is.LessThan(_tolerance), "t=" + t);
                Assert.That(flight.Velocity.Y, Is.EqualTo(_gravity.Y * gravityMultiplier * t).Within(_tolerance));
                Assert.That(flight.Age, Is.EqualTo(t).Within(1e-4f));
            }

            Assert.That(flight.State, Is.EqualTo(BallisticState.Flying));
        }

        [Test]
        public void WithoutGravityItFliesStraightAtItsSpeed()
        {
            var flight = Flight(new ProjectileSpec(30f, 0f, 10f), new FakeWorldCollision(), new Vector3(0f, 0f, 2f));

            var step = flight.Step(0.5f);

            Assert.That(Vector3.Distance(step.From, Vector3.Zero), Is.LessThan(_tolerance));
            Assert.That(Vector3.Distance(step.To, new Vector3(0f, 0f, 15f)), Is.LessThan(_tolerance));
            Assert.That(flight.Distance, Is.EqualTo(15f).Within(_tolerance));
            Assert.That(step.HasHit, Is.False);
        }

        [TestCase(WorldHitResponse.Stop, BallisticState.Stopped)]
        [TestCase(WorldHitResponse.Stick, BallisticState.Stuck)]
        public void AWallEndsTheFlightWhereItWasHit(WorldHitResponse response, BallisticState expected)
        {
            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-5f, -5f, 10f), new Vector3(5f, 5f, 11f));
            var flight = Flight(new ProjectileSpec(20f, 0f, 10f, response), world, Vector3.UnitZ);

            var first = flight.Step(0.25f);
            var second = flight.Step(0.5f);

            Assert.That(first.State, Is.EqualTo(BallisticState.Flying));
            Assert.That(second.State, Is.EqualTo(expected));
            Assert.That(second.HasHit, Is.True);
            Assert.That(Vector3.Distance(flight.Position, new Vector3(0f, 0f, 10f)), Is.LessThan(_tolerance));
            Assert.That(Vector3.Distance(second.Hit.Normal, -Vector3.UnitZ), Is.LessThan(_tolerance));
            Assert.That(flight.Age, Is.EqualTo(0.5f).Within(1e-4f), "the flight stops ageing at the wall");
            Assert.That(flight.Distance, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(flight.IsDone, Is.True);
            Assert.Throws<InvalidOperationException>(() => flight.Step(0.1f));
        }

        [Test]
        public void ADroppingRoundHitsTheWallLowerThanItWasAimed()
        {
            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-5f, -50f, 20f), new Vector3(5f, 50f, 21f));
            var flight = Flight(new ProjectileSpec(20f, 1f, 10f), world, Vector3.UnitZ);

            while (!flight.IsDone)
            {
                flight.Step(0.02f);
            }

            Assert.That(flight.State, Is.EqualTo(BallisticState.Stopped));
            Assert.That(flight.Position.Z, Is.EqualTo(20f).Within(_tolerance));
            Assert.That(flight.Position.Y, Is.EqualTo(-5f).Within(0.05f), "1 s to the wall drops 0.5 * 10 * 1^2 m");
        }

        [Test]
        public void ABouncingRoundComesBackOffTheWallAndKeepsFlying()
        {
            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-5f, -5f, 10f), new Vector3(5f, 5f, 11f));
            var flight = Flight(new ProjectileSpec(20f, 0f, 10f, WorldHitResponse.Bounce), world, Vector3.UnitZ);

            var step = flight.Step(0.75f);

            Assert.That(step.State, Is.EqualTo(BallisticState.Flying));
            Assert.That(step.HasHit, Is.True);
            Assert.That(flight.Bounces, Is.EqualTo(1));
            Assert.That(Vector3.Distance(flight.Velocity, new Vector3(0f, 0f, -20f)), Is.LessThan(_tolerance));
            Assert.That(flight.Position.Z, Is.EqualTo(5f).Within(0.01f), "the rest of the step is flown back from the wall");
            Assert.That(flight.Age, Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void TheLifetimeEndsTheFlightAtExactlyItsLength()
        {
            var flight = Flight(new ProjectileSpec(10f, 1f, 1f), new FakeWorldCollision(), Vector3.UnitZ);

            Assert.That(flight.Step(0.3f).State, Is.EqualTo(BallisticState.Flying));
            Assert.That(flight.Step(0.3f).State, Is.EqualTo(BallisticState.Flying));
            Assert.That(flight.Step(0.3f).State, Is.EqualTo(BallisticState.Flying));
            var last = flight.Step(0.3f);

            Assert.That(last.State, Is.EqualTo(BallisticState.Expired));
            Assert.That(flight.Age, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(flight.Position, new Vector3(0f, -5f, 10f)), Is.LessThan(_tolerance));
            Assert.Throws<InvalidOperationException>(() => flight.Step(0.1f));
        }

        [Test]
        public void ATargetEndsTheFlightEvenForABouncingRound()
        {
            var world = new FakeWorldCollision();
            var target = world.AddBox(new Vector3(-1f, -1f, 5f), new Vector3(1f, 1f, 6f), layer: 3);
            var flight = Flight(
                new ProjectileSpec(20f, 0f, 10f, WorldHitResponse.Bounce), world, Vector3.UnitZ, _everything, hit => hit.Layer == 3);

            var step = flight.Step(1f);

            Assert.That(step.State, Is.EqualTo(BallisticState.HitTarget));
            Assert.That(step.Hit.ColliderId, Is.EqualTo(target));
            Assert.That(flight.Bounces, Is.Zero);
        }

        [Test]
        public void TheLayerMaskDecidesWhatTheRoundCanHit()
        {
            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-5f, -5f, 10f), new Vector3(5f, 5f, 11f), layer: 2);
            var flight = Flight(new ProjectileSpec(20f, 0f, 10f), world, Vector3.UnitZ, ~(1 << 2));

            var step = flight.Step(1f);

            Assert.That(step.HasHit, Is.False);
            Assert.That(flight.Position.Z, Is.EqualTo(20f).Within(_tolerance));
        }

        [Test]
        public void AChargedShotFliesFaster()
        {
            var spec = new ProjectileSpec(20f, 0f, 5f);
            var charge = new ChargeSpec(1f, 0f, 1f, fullChargeSpeedMultiplier: 2f);
            var flight = new BallisticFlight(
                spec, null, new FakeWorldCollision(), _everything, Vector3.Zero, Vector3.UnitZ, _gravity, null, ChargeScaling.SpeedMultiplier(charge, 1f));

            Assert.That(flight.Velocity.Z, Is.EqualTo(40f).Within(_tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallisticFlight(spec, null, new FakeWorldCollision(), _everything, Vector3.Zero, Vector3.UnitZ, _gravity, null, 0f));
        }

        [Test]
        public void RejectsInputsItCannotFly()
        {
            var spec = new ProjectileSpec(20f, 1f, 1f);
            var world = new FakeWorldCollision();

            Assert.Throws<ArgumentNullException>(() => new BallisticFlight(null, null, world, _everything, Vector3.Zero, Vector3.UnitZ, _gravity));
            Assert.Throws<ArgumentNullException>(() => new BallisticFlight(spec, null, null, _everything, Vector3.Zero, Vector3.UnitZ, _gravity));
            Assert.Throws<ArgumentException>(() => new BallisticFlight(spec, null, world, _everything, Vector3.Zero, Vector3.Zero, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallisticFlight(spec, null, world, _everything, new Vector3(float.NaN, 0f, 0f), Vector3.UnitZ, _gravity));
            var flight = Flight(spec, world, Vector3.UnitZ);
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(float.PositiveInfinity));
        }

        private static BallisticFlight Flight(
            ProjectileSpec spec, IWorldCollision world, Vector3 direction, int layerMask = _everything, Func<WorldHit, bool> isTarget = null)
        {
            return new BallisticFlight(spec, null, world, layerMask, Vector3.Zero, direction, _gravity, isTarget);
        }
    }
}
