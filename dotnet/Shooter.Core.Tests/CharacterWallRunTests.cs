using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterWallRunTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;

        // 中心の高さが 0 の体にして、壁を調べる高さを本体の位置と同じにする
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 _rightWallNormal = new Vector3(-1f, 0f, 0f);
        private static readonly WallRunTuning _enabled = new WallRunTuning(true);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheSensorFindsAWallOnTheRight()
        {
            AddRightWall();
            var sensor = new CharacterWallSensor(NewBody());

            var contact = sensor.Probe(new Vector3(0f, 2f, 0f), 0f);

            Assert.That(contact.Side, Is.EqualTo(WallRunSide.Right));
            AssertNormal(contact.Normal, _rightWallNormal);
            Assert.That(contact.Distance, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(sensor.Reach, Is.EqualTo(0.8f).Within(1e-6f));
        }

        [Test]
        public void TheSensorSeesTheSameWallOnTheLeftWhenFacingTheOtherWay()
        {
            AddRightWall();
            var sensor = new CharacterWallSensor(NewBody());

            var contact = sensor.Probe(new Vector3(0f, 2f, 0f), MathF.PI);

            Assert.That(contact.Side, Is.EqualTo(WallRunSide.Left));
            AssertNormal(contact.Normal, _rightWallNormal);
        }

        [Test]
        public void TheSensorTakesTheNearerOfTwoWalls()
        {
            AddRightWall();
            AddBox(new Vector3(-0.85f, 4f, 0f), new Vector3(0.25f, 4f, 10f));
            var sensor = new CharacterWallSensor(NewBody());

            var contact = sensor.Probe(new Vector3(0f, 2f, 0f), 0f);

            Assert.That(contact.Side, Is.EqualTo(WallRunSide.Left));
            AssertNormal(contact.Normal, new Vector3(1f, 0f, 0f));
            Assert.That(contact.Distance, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void TheSensorIgnoresAWallOutOfReach()
        {
            AddRightWall();
            var sensor = new CharacterWallSensor(NewBody());

            Assert.That(sensor.Probe(new Vector3(-0.2f, 2f, 0f), 0f).IsFound, Is.False, "0.9 m away is past 0.5 + 0.3");
        }

        [Test]
        public void TheSensorIgnoresASlantedFace()
        {
            const float angle = 0.5f;
            var center = new Vector3(0.6f + (0.25f * MathF.Cos(angle)), 2f + (0.25f * MathF.Sin(angle)), 0f);
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1, 0, WorldCollider.StaticGroup, center, new Vector3(0.25f, 3f, 10f), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle)));
            var sensor = new CharacterWallSensor(NewBody());

            Assert.That(sensor.Probe(new Vector3(0f, 2f, 0f), 0f).IsFound, Is.False, "a face tilted about 29 degrees is not a wall");
        }

        [Test]
        public void TheSensorFollowsTheWallUntilItEnds()
        {
            AddRightWall();
            var sensor = new CharacterWallSensor(NewBody());

            var beside = sensor.Follow(new Vector3(0f, 2f, 9f), WallRunSide.Right, _rightWallNormal);
            var past = sensor.Follow(new Vector3(0f, 2f, 10.5f), WallRunSide.Right, _rightWallNormal);

            Assert.That(beside.Side, Is.EqualTo(WallRunSide.Right));
            AssertNormal(beside.Normal, _rightWallNormal);
            Assert.That(past.IsFound, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => sensor.Follow(Vector3.Zero, WallRunSide.None, _rightWallNormal));
            Assert.Throws<ArgumentNullException>(() => new CharacterWallSensor(null));
        }

        [Test]
        public void TheSimulationStartsARunBesideAWall()
        {
            AddWorld();
            var plain = NewSimulation(WallRunTuning.Default);
            var simulation = NewSimulation(_enabled);
            var start = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f));

            var next = simulation.Step(start, Forward());
            var notRunning = plain.Step(start, Forward());

            Assert.That(next.IsWallRunning, Is.True);
            Assert.That(next.WallRun.Side, Is.EqualTo(WallRunSide.Right));
            AssertNormal(next.WallRun.Normal, _rightWallNormal);
            Assert.That(next.WallRun.Elapsed, Is.EqualTo(_tick));
            Assert.That(next.Velocity.Y, Is.EqualTo(2f - (20f * 0.25f * _tick)).Within(1e-4f), "a quarter of the gravity");
            Assert.That(next.Velocity.Z, Is.EqualTo(8f).Within(1e-3f));
            Assert.That(next.Position.Z, Is.EqualTo(-8f + (8f * _tick)).Within(1e-3f));
            Assert.That(notRunning.IsWallRunning, Is.False, "switched off by default");
            Assert.That(notRunning.Velocity.Y, Is.EqualTo(2f - (20f * _tick)).Within(1e-4f));
        }

        [Test]
        public void TheSimulationKeepsRunningAlongTheWall()
        {
            AddWorld();
            var simulation = NewSimulation(_enabled);
            var state = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f));

            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Forward());
                Assert.That(state.IsWallRunning, Is.True, "tick " + i);
            }

            Assert.That(state.WallRun.Elapsed, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(state.Position.X, Is.EqualTo(0f).Within(0.05f), "it stays beside the wall");
            Assert.That(state.Position.Z, Is.EqualTo(-4f).Within(0.05f), "8 m/s for half a second");
            Assert.That(state.Position.Y, Is.EqualTo(3f + 1f - 0.625f).Within(0.02f), "2 m/s up under 5 m/s^2 for half a second");
        }

        [Test]
        public void TheSimulationEndsTheRunAfterTheLongestTime()
        {
            AddWorld();
            var simulation = NewSimulation(new WallRunTuning(true, 0.25f));
            var state = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f));

            var runningTicks = 0;
            for (var i = 0; i < 40; i++)
            {
                state = simulation.Step(state, Forward());
                if (!state.IsWallRunning)
                {
                    break;
                }

                runningTicks++;
            }

            Assert.That(state.IsWallRunning, Is.False);
            Assert.That(runningTicks, Is.InRange(15, 16), "0.25 seconds at 60 ticks a second");
            AssertNormal(state.WallRun.LastWall, _rightWallNormal);
        }

        [Test]
        public void TheSimulationEndsTheRunWhereTheWallEnds()
        {
            AddWorld();
            var simulation = NewSimulation(_enabled);
            var state = Airborne(new Vector3(0f, 3f, 8f), new Vector3(0f, 1f, 8f));

            state = simulation.Step(state, Forward());
            Assert.That(state.IsWallRunning, Is.True);
            for (var i = 0; i < 60 && state.IsWallRunning; i++)
            {
                state = simulation.Step(state, Forward());
            }

            Assert.That(state.IsWallRunning, Is.False);
            Assert.That(state.Position.Z, Is.GreaterThan(10f), "it ran to the end of the wall");
            Assert.That(state.Position.Z, Is.LessThan(10.5f));
        }

        [Test]
        public void TheSimulationEndsTheRunOnBackwardInputAndDoesNotRejoinTheSameWall()
        {
            AddWorld();
            var simulation = NewSimulation(_enabled);
            var state = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f));
            for (var i = 0; i < 3; i++)
            {
                state = simulation.Step(state, Forward());
            }

            Assert.That(state.IsWallRunning, Is.True);
            state = simulation.Step(state, Input(new Vector2(0f, -1f)));
            Assert.That(state.IsWallRunning, Is.False);

            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Forward());
                Assert.That(state.IsWallRunning, Is.False, "tick " + i);
            }

            Assert.That(state.Velocity.Z, Is.GreaterThan(5f), "still fast enough, so only the same wall stops it");
        }

        [Test]
        public void TheSimulationJumpsAwayFromTheWallAndGivesBackTheAirJumps()
        {
            AddWorld();
            var motor = new PlayerMotor(MotorSpec.Default);
            var jumped = 0;
            motor.Jumped += () => jumped++;
            var simulation = NewSimulation(_enabled, motor);
            var state = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f), airJumpsUsed: 1);
            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Forward());
            }

            Assert.That(state.IsWallRunning, Is.True);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1), "running does not give back the air jumps");

            state = simulation.Step(state, Input(new Vector2(0f, 1f), jump: true));

            Assert.That(state.IsWallRunning, Is.False);
            Assert.That(state.Velocity.X, Is.EqualTo(-7f).Within(1e-4f), "away from the wall at the jump-off speed");
            Assert.That(state.Velocity.Y, Is.EqualTo(MathF.Sqrt(48f) - (20f * _tick)).Within(1e-3f), "as high as a ground jump");
            Assert.That(state.Velocity.Z, Is.EqualTo(8f).Within(1e-2f));
            Assert.That(state.AirJumpsUsed, Is.Zero);
            Assert.That(jumped, Is.EqualTo(1));
            AssertNormal(state.WallRun.LastWall, _rightWallNormal);
        }

        [Test]
        public void TheSimulationDoesNotStartWhenSwitchedOffOrTooSlow()
        {
            AddWorld();
            var off = NewSimulation(WallRunTuning.Default);
            var on = NewSimulation(_enabled);
            var fast = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f));
            var slow = Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 3f));

            for (var i = 0; i < 10; i++)
            {
                fast = off.Step(fast, Forward());
                slow = on.Step(slow, Forward());
                Assert.That(fast.IsWallRunning, Is.False, "switched off, tick " + i);
                Assert.That(slow.IsWallRunning, Is.False, "too slow, tick " + i);
            }
        }

        [Test]
        public void TheSimulationDoesNotStartWithoutForwardInput()
        {
            AddWorld();
            var simulation = NewSimulation(_enabled);

            var next = simulation.Step(Airborne(new Vector3(0f, 3f, -8f), new Vector3(0f, 2f, 8f)), Input(Vector2.Zero));

            Assert.That(next.IsWallRunning, Is.False);
        }

        [Test]
        public void LandingForgetsTheLastWall()
        {
            AddWorld();
            var simulation = NewSimulation(_enabled);
            var s = new CharacterMotorState(new Vector3(-5f, 1.2f, 0f), Vector3.Zero, false, false);
            var state = new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, s.Ladder, s.Knockback,
                s.Lean, s.Swim, CharacterWallRunState.LeftWall(_rightWallNormal), 0);

            for (var i = 0; i < 30 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            state = simulation.Step(state, Input(Vector2.Zero));

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.WallRun, Is.EqualTo(CharacterWallRunState.None));
        }

        private static CharacterMotorState Airborne(Vector3 position, Vector3 velocity, int airJumpsUsed = 0)
        {
            var s = new CharacterMotorState(position, velocity, false, false);
            return new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, s.Ladder, s.Knockback,
                s.Lean, s.Swim, s.WallRun, airJumpsUsed);
        }

        private static CharacterMotorInput Forward()
        {
            return Input(new Vector2(0f, 1f));
        }

        private static CharacterMotorInput Input(Vector2 move, bool jump = false)
        {
            return new CharacterMotorInput(move, 0f, false, jump, false, false, false, false, false, false);
        }

        private static void AssertNormal(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"normal {actual} should be {expected}");
        }

        private CharacterBody NewBody()
        {
            return new CharacterBody(new BvhWorldCollision(new WorldMap(_colliders)), CapsuleMoverSpec.Default, _stance);
        }

        private CharacterMotorSimulation NewSimulation(WallRunTuning tuning, PlayerMotor motor = null)
        {
            return new CharacterMotorSimulation(NewBody(), motor ?? new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY)
            {
                SpawnPosition = _spawn,
                WallRun = tuning,
            };
        }

        private void AddWorld()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
            AddRightWall();
        }

        // 体の軸から 0.7 m 右 (+X) に面を向けた、Z が -10 から 10 までの壁
        private void AddRightWall()
        {
            AddBox(new Vector3(0.95f, 4f, 0f), new Vector3(0.25f, 4f, 10f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
