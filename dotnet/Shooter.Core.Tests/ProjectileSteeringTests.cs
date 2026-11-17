using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ProjectileSteeringTests
    {
        private static readonly HomingSpec _homing = new HomingSpec(1f, 10f, turnDegreesPerSecond: 90f);

        [Test]
        public void TurnsNoFasterThanTheTurnRateAndKeepsTheSpeed()
        {
            var velocity = new Vector3(0f, 0f, 30f);

            // 真横の相手へは 0.1 秒で 9 度しか曲がれない
            var steered = ProjectileSteering.Steer(_homing, Vector3.Zero, velocity, new Vector3(10f, 0f, 0f), 0.1f);

            Assert.That(steered.Length(), Is.EqualTo(30f).Within(1e-3f));
            Assert.That(Degrees(velocity, steered), Is.EqualTo(9f).Within(1e-2f));
            Assert.That(steered.X, Is.GreaterThan(0f), "turns toward the target");
            Assert.That(steered.Y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void PointsStraightAtATargetWithinOneStepsTurn()
        {
            var steered = ProjectileSteering.Steer(_homing, Vector3.Zero, new Vector3(0f, 0f, 20f), new Vector3(1f, 0f, 20f), 0.1f);

            Assert.That(Vector3.Distance(Vector3.Normalize(steered), Vector3.Normalize(new Vector3(1f, 0f, 20f))), Is.LessThan(1e-5f));
            Assert.That(steered.Length(), Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void TurnsAwayFromStraightAheadEvenForATargetDirectlyBehind()
        {
            var velocity = new Vector3(0f, 0f, 10f);
            var steered = ProjectileSteering.Steer(_homing, Vector3.Zero, velocity, new Vector3(0f, 0f, -5f), 0.5f);

            Assert.That(Degrees(velocity, steered), Is.EqualTo(45f).Within(1e-2f));
            Assert.That(steered.Length(), Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void WithoutATargetOrSpeedTheVelocityIsKept()
        {
            var velocity = new Vector3(1f, 2f, 3f);

            Assert.That(ProjectileSteering.Steer(_homing, Vector3.Zero, velocity, null, 0.1f), Is.EqualTo(velocity));
            Assert.That(ProjectileSteering.Steer(_homing, Vector3.One, velocity, Vector3.One, 0.1f), Is.EqualTo(velocity), "already there");
            Assert.That(ProjectileSteering.Steer(_homing, Vector3.Zero, Vector3.Zero, Vector3.UnitX, 0.1f), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => ProjectileSteering.Steer(null, Vector3.Zero, Vector3.UnitZ, Vector3.UnitX, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProjectileSteering.Steer(_homing, Vector3.Zero, Vector3.UnitZ, Vector3.UnitX, float.NaN));
        }

        [Test]
        public void AHomingFlightCurvesOntoAMovingTarget()
        {
            var projectile = new ProjectileSpec(speed: 30f, gravityMultiplier: 0f, lifetimeSeconds: 5f);
            var world = new FakeWorldCollision();
            var flight = new BallisticFlight(projectile, _homing, world, ~0, Vector3.Zero, Vector3.UnitZ, Vector3.Zero);
            var target = new Vector3(15f, 0f, 30f);
            var closest = float.MaxValue;

            for (var i = 0; i < 180 && !flight.IsDone; i++)
            {
                target += new Vector3(0f, 0f, 3f / 60f);
                flight.Step(1f / 60f, target);
                closest = Math.Min(closest, Vector3.Distance(flight.Position, target));
            }

            Assert.That(closest, Is.LessThan(0.5f), "the round reaches a target it would miss flying straight");
        }

        private static float Degrees(Vector3 a, Vector3 b)
        {
            var cos = Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b));
            return (float)(Math.Acos(Math.Clamp(cos, -1f, 1f)) * 180.0 / Math.PI);
        }
    }
}
