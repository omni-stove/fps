using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorKnockbackTests
    {
        private const float _tick = 1f / 60f;
        private const int _settleTicks = 30;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        [Test]
        public void LaunchingStartsAFlightAtZeroSecondsAndAddsToOneInProgress()
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, true, false);

            var launched = CharacterMotorSimulation.Launch(state, new Vector3(3f, 2f, 0f));
            var again = CharacterMotorSimulation.Launch(launched.WithKnockback(CharacterKnockbackState.Flying(launched.Knockback.Velocity, 0.2f)), new Vector3(1f, 0f, 1f));

            Assert.That(launched.IsKnockedBack, Is.True);
            Assert.That(launched.Knockback.Velocity, Is.EqualTo(new Vector3(3f, 2f, 0f)));
            Assert.That(launched.Knockback.ElapsedSeconds, Is.EqualTo(0f));
            Assert.That(launched.Position, Is.EqualTo(state.Position), "it moves from the next step");
            Assert.That(again.Knockback.Velocity, Is.EqualTo(new Vector3(4f, 2f, 1f)));
            Assert.That(again.Knockback.ElapsedSeconds, Is.EqualTo(0.2f));
        }

        [Test]
        public void TheFlightFollowsKnockbackFlightWithTheSpecGravity()
        {
            var simulation = NewSimulation(new KnockbackSpec(gravity: 10f));
            var state = CharacterMotorSimulation.Launch(Settle(simulation), new Vector3(4f, 5f, 0f));
            var start = state.Position;
            var flight = new KnockbackFlight();
            flight.Launch(new Vector3(4f, 5f, 0f));
            var expected = Vector3.Zero;

            for (var i = 0; i < 20; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
                expected += flight.Step(_tick, 10f);
            }

            Assert.That(state.IsKnockedBack, Is.True);
            Assert.That(state.Knockback.ElapsedSeconds, Is.EqualTo(20 * _tick).Within(1e-5f));
            Assert.That(state.Knockback.Velocity.Y, Is.EqualTo(5f - (10f * 20 * _tick)).Within(1e-4f));
            Assert.That(state.Velocity, Is.EqualTo(state.Knockback.Velocity));
            Assert.That((state.Position - start).X, Is.EqualTo(expected.X).Within(1e-3f));
            Assert.That((state.Position - start).Y, Is.EqualTo(expected.Y).Within(1e-3f));
        }

        [Test]
        public void MovingInTheAirSteersTheFlightByTheAirControl()
        {
            var free = NewSimulation(new KnockbackSpec(airControl: 2f));
            var steered = NewSimulation(new KnockbackSpec(airControl: 2f));
            var a = CharacterMotorSimulation.Launch(Settle(free), new Vector3(0f, 6f, 0f));
            var b = CharacterMotorSimulation.Launch(Settle(steered), new Vector3(0f, 6f, 0f));

            for (var i = 0; i < 20; i++)
            {
                a = free.Step(a, Input(Vector2.Zero));
                b = steered.Step(b, Input(new Vector2(0f, 1f)));
            }

            Assert.That(a.Position.Z, Is.EqualTo(_start.Z).Within(1e-4f), "no input does not stop or push the flight");
            Assert.That(b.Position.Z, Is.GreaterThan(a.Position.Z + 0.1f), "forward input steers toward the walk speed");
            Assert.That(b.Knockback.Velocity.Z, Is.LessThan(MotorSpec.DefaultWalkSpeed));
        }

        [Test]
        public void TheFlightEndsWhenItLandsAfterTheMinimumAirTime()
        {
            var simulation = NewSimulation(KnockbackSpec.Default);
            var state = CharacterMotorSimulation.Launch(Settle(simulation), new Vector3(2f, 4f, 0f));
            var flew = 0;
            while (state.IsKnockedBack && flew < 200)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
                flew++;
            }

            // 4 m/s を重力 20 で上げると 0.4 秒で戻る。着地の tick の次に飛び終える
            Assert.That(state.IsKnockedBack, Is.False);
            Assert.That(flew * _tick, Is.EqualTo(0.4f).Within(3f * _tick));
            Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.X - _start.X, Is.EqualTo(0.8f).Within(0.1f));

            state = simulation.Step(state, Input(Vector2.Zero));
            Assert.That(state.IsGrounded, Is.True, "the motor takes over on the floor");
        }

        [Test]
        public void ATimedOutFlightHandsItsFallingSpeedToTheMotor()
        {
            var simulation = NewSimulation(new KnockbackSpec(minAirSeconds: 0.05f, maxAirSeconds: 0.1f));
            var state = CharacterMotorSimulation.Launch(Settle(simulation), new Vector3(0f, 8f, 0f));
            while (state.IsKnockedBack)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.Velocity.Y, Is.EqualTo(8f - (KnockbackSpec.DefaultGravity * 0.1f)).Within(KnockbackSpec.DefaultGravity * _tick));
        }

        [Test]
        public void BeingLaunchedStopsTheDodgeAndTheProne()
        {
            var simulation = NewSimulation(KnockbackSpec.Default);
            var state = Settle(simulation);
            state = simulation.Step(state, new CharacterMotorInput(new Vector2(0f, 1f), 0f, false, false, false, false, false, false, true, false));
            Assert.That(state.IsDodging, Is.True);

            state = simulation.Step(CharacterMotorSimulation.Launch(state, new Vector3(0f, 5f, 0f)), Input(Vector2.Zero));

            Assert.That(state.IsDodging, Is.False);
            Assert.That(state.IsProne, Is.False);
            Assert.That(state.IsKnockedBack, Is.True);
        }

        [Test]
        public void ReplayingFromTheSameFlightGivesTheSameStates()
        {
            var first = NewSimulation(KnockbackSpec.Default);
            var second = NewSimulation(KnockbackSpec.Default);
            var launched = CharacterMotorSimulation.Launch(Settle(first), new Vector3(3f, 4f, 1f));
            var a = launched;
            var b = launched;

            for (var i = 0; i < 40; i++)
            {
                var input = Input(new Vector2(MathF.Sin(i * 0.3f), 1f), i * 0.02f);
                a = first.Step(a, input);
                b = second.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
            }
        }

        [Test]
        public void FlyingAgainstWalkingIsAlwaysAMismatch()
        {
            var walking = new CharacterMotorState(_start, Vector3.Zero, true, false);
            var flying = CharacterMotorSimulation.Launch(walking, new Vector3(1f, 1f, 0f));

            Assert.That(CharacterMotorState.PositionError(walking, flying), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(flying, flying), Is.EqualTo(0f));
            Assert.That(flying.IsFinite, Is.True);
            Assert.That(flying, Is.Not.EqualTo(walking));
        }

        [Test]
        public void TheKnockbackSpecMustBeSet()
        {
            var simulation = NewSimulation(KnockbackSpec.Default);

            Assert.Throws<ArgumentNullException>(() => simulation.Knockback = null);
        }

        [Test]
        public void ARestoredFlightContinuesFromItsSeconds()
        {
            var flight = new KnockbackFlight();
            flight.Restore(CharacterKnockbackState.Flying(new Vector3(1f, 2f, 3f), 0.5f));

            Assert.That(flight.IsFlying, Is.True);
            Assert.That(flight.ElapsedSeconds, Is.EqualTo(0.5f));
            Assert.That(flight.Capture(), Is.EqualTo(CharacterKnockbackState.Flying(new Vector3(1f, 2f, 3f), 0.5f)));

            flight.Restore(CharacterKnockbackState.None);
            Assert.That(flight.IsFlying, Is.False);
            Assert.That(flight.Capture(), Is.EqualTo(CharacterKnockbackState.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterKnockbackState.Flying(Vector3.UnitY, -1f));
        }

        private static CharacterMotorInput Input(Vector2 move, float yaw = 0f)
        {
            return new CharacterMotorInput(move, yaw, false, false, false, false, false, false, false, false);
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private CharacterMotorSimulation NewSimulation(KnockbackSpec knockback)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f)
            {
                SpawnPosition = _start,
                Knockback = knockback,
            };
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
