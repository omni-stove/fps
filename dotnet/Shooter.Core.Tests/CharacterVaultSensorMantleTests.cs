using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterVaultSensorMantleTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 30;

        // 体の正面 0.8 m に面がある
        private const float _faceZ = 0.8f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void AChestHighLedgeCanBeMantled()
        {
            AddFloor();
            AddStep(1.5f, 4f);
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var feetY = simulation.Body.FeetY(state.Position);

            var result = simulation.VaultSensor.ProbeMantle(state.Position, 0f, MotorSpec.Default);

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.Mantle));
            Assert.That(result.Height, Is.EqualTo(1.5f - feetY).Within(1e-3f));
            Assert.That(result.Depth, Is.EqualTo(float.PositiveInfinity), "deeper than the footing is measured as inside");
            Assert.That(result.Direction.Z, Is.EqualTo(1f).Within(1e-4f));
            var points = result.Path.Points;
            Assert.That(points[points.Count - 1].Y, Is.EqualTo(result.Height).Within(1e-4f), "the path ends standing on the top");
            Assert.That(points[points.Count - 1].Z, Is.EqualTo(_faceZ + _stance.Radius + VaultProbe.WallGap).Within(1e-3f));
            Assert.That(result.Path.Duration * MotorSpec.DefaultMantleSpeed, Is.GreaterThan(result.Height), "follows at the mantle speed");
        }

        [Test]
        public void TheVaultProbeStillRejectsTheSameLedge()
        {
            AddFloor();
            AddStep(1.5f, 4f);
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var result = simulation.VaultSensor.Probe(state.Position, 0f, MotorSpec.Default);

            Assert.That(result.CanVault, Is.False);
        }

        [TestCase(0.8f, 4f, VaultVerdict.TooLow)]
        [TestCase(3f, 4f, VaultVerdict.TooHigh)]
        [TestCase(1.5f, 0.4f, VaultVerdict.TooNarrow)]
        public void ALedgeOutsideTheMantleLimitsIsRejected(float height, float depth, VaultVerdict expected)
        {
            AddFloor();
            AddStep(height, depth);
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var result = simulation.VaultSensor.ProbeMantle(state.Position, 0f, MotorSpec.Default);

            Assert.That(result.Verdict, Is.EqualTo(expected));
            Assert.That(result.CanMantle, Is.False);
            Assert.That(result.Path, Is.Null);
        }

        [Test]
        public void TheMeasuredDepthOfAShallowStepIsItsThickness()
        {
            AddFloor();
            AddStep(1.5f, 0.4f);
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var result = simulation.VaultSensor.ProbeMantle(state.Position, 0f, MotorSpec.Default);

            Assert.That(result.Depth, Is.EqualTo(0.4f).Within(1e-3f));
        }

        [Test]
        public void TheConfiguredMantleHeightLimitsTheReach()
        {
            AddFloor();
            AddStep(1.5f, 4f);
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var result = simulation.VaultSensor.ProbeMantle(state.Position, 0f, new MotorSpec(mantleMaxHeight: 1.3f));

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.TooHigh));
        }

        [Test]
        public void FacingAwayFindsNothing()
        {
            AddFloor();
            AddStep(1.5f, 4f);
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var result = simulation.VaultSensor.ProbeMantle(state.Position, MathF.PI, MotorSpec.Default);

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.NoObstacle));
        }

        [Test]
        public void ANullSpecIsRejected()
        {
            AddFloor();
            var simulation = NewSimulation();

            Assert.Throws<ArgumentNullException>(() => simulation.VaultSensor.ProbeMantle(_start, 0f, null));
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            var input = new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, false, false, false, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, input);
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private CharacterMotorSimulation NewSimulation()
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY)
            {
                SpawnPosition = _start,
            };
        }

        // 床の上面は y=0
        private void AddFloor()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        private void AddStep(float height, float depth)
        {
            AddBox(new Vector3(0f, height / 2f, _faceZ + (depth / 2f)), new Vector3(3f, height / 2f, depth / 2f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
