using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorTraversalStateTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheOlderConstructorsLeaveTheNewStatesEmpty()
        {
            var state = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);

            Assert.That(state.Lean, Is.EqualTo(CharacterLeanState.None));
            Assert.That(state.Swim, Is.EqualTo(CharacterSwimState.None));
            Assert.That(state.WallRun, Is.EqualTo(CharacterWallRunState.None));
            Assert.That(state.AirJumpsUsed, Is.Zero);
            Assert.That(state.IsSwimming, Is.False);
            Assert.That(state.IsWallRunning, Is.False);
        }

        [Test]
        public void EqualsSeesEachNewState()
        {
            var plain = With();

            Assert.That(With(), Is.EqualTo(plain));
            Assert.That(With(lean: new CharacterLeanState(0.5f)), Is.Not.EqualTo(plain));
            Assert.That(With(swim: new CharacterSwimState(true, 1f, false)), Is.Not.EqualTo(plain));
            Assert.That(With(wallRun: new CharacterWallRunState(WallRunSide.Left, 0.1f, Vector3.UnitX)), Is.Not.EqualTo(plain));
            Assert.That(With(airJumpsUsed: 1), Is.Not.EqualTo(plain));
            var used = With(airJumpsUsed: 1);
            var sameUsed = With(airJumpsUsed: 1);
            Assert.That(used.GetHashCode(), Is.EqualTo(sameUsed.GetHashCode()));
        }

        [Test]
        public void WithKnockbackKeepsTheNewStates()
        {
            var state = With(
                new CharacterLeanState(-0.25f), new CharacterSwimState(true, 2f, true), new CharacterWallRunState(WallRunSide.Right, 0.3f, -Vector3.UnitX), 2);

            var launched = state.WithKnockback(CharacterKnockbackState.None.Launch(new Vector3(0f, 5f, 0f)));

            Assert.That(launched.Lean, Is.EqualTo(state.Lean));
            Assert.That(launched.Swim, Is.EqualTo(state.Swim));
            Assert.That(launched.WallRun, Is.EqualTo(state.WallRun));
            Assert.That(launched.AirJumpsUsed, Is.EqualTo(2));
        }

        [Test]
        public void TheNewStatesForgetWhatTheyDoNotUse()
        {
            Assert.That(new CharacterSwimState(false, 3f, true), Is.EqualTo(CharacterSwimState.None), "the surface is only kept while swimming");
            Assert.That(
                new CharacterWallRunState(WallRunSide.None, 0.4f, Vector3.UnitZ), Is.EqualTo(CharacterWallRunState.None), "the time and wall are only kept while running");
            Assert.That(
                new CharacterWallRunState(WallRunSide.Left, 0.4f, Vector3.UnitX, Vector3.UnitZ).LastWall,
                Is.EqualTo(Vector3.Zero),
                "the last wall is only kept while not running");
            Assert.That(CharacterWallRunState.LeftWall(Vector3.UnitZ), Is.Not.EqualTo(CharacterWallRunState.None));
            Assert.That(CharacterWallRunState.LeftWall(Vector3.UnitZ).LastWall, Is.EqualTo(Vector3.UnitZ));
            Assert.That(CharacterWallRunState.LeftWall(new Vector3(float.NaN, 0f, 0f)).IsValid, Is.False);
        }

        [Test]
        public void PositionErrorRewindsWhenAMovementStateDiffers()
        {
            var plain = With();

            Assert.That(CharacterMotorState.PositionError(plain, With(swim: new CharacterSwimState(true, 1f, false))), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(
                CharacterMotorState.PositionError(plain, With(wallRun: new CharacterWallRunState(WallRunSide.Left, 0.1f, Vector3.UnitX))),
                Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(plain, With(airJumpsUsed: 1)), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(plain, With(lean: new CharacterLeanState(-0.2f))), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(
                CharacterMotorState.PositionError(With(lean: new CharacterLeanState(0.3f)), With(lean: new CharacterLeanState(0.8f))),
                Is.Zero,
                "leaning the same way by a different amount catches up with the same inputs");
        }

        [Test]
        public void IsFiniteRejectsABrokenNewState()
        {
            Assert.That(With().IsFinite, Is.True);
            Assert.That(With(lean: new CharacterLeanState(1.5f)).IsFinite, Is.False);
            Assert.That(With(lean: new CharacterLeanState(float.NaN)).IsFinite, Is.False);
            Assert.That(With(swim: new CharacterSwimState(true, float.PositiveInfinity, false)).IsFinite, Is.False);
            Assert.That(With(wallRun: new CharacterWallRunState(WallRunSide.Left, 0.1f, Vector3.Zero)).IsFinite, Is.False, "a wall run needs its wall");
            Assert.That(With(wallRun: new CharacterWallRunState(WallRunSide.Right, -1f, Vector3.UnitX)).IsFinite, Is.False);
            Assert.That(With(airJumpsUsed: -1).IsFinite, Is.False);
        }

        [Test]
        public void TheLeanInputIsMinusOneZeroOrOne()
        {
            var input = new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, false, false, false, false, false);

            Assert.That(default(CharacterMotorInput).Lean, Is.Zero);
            Assert.That(input.Lean, Is.Zero);
            Assert.That(input.WithLean(-1).Lean, Is.EqualTo(-1));
            Assert.That(input.With(lean: 1).Lean, Is.EqualTo(1));
            Assert.That(input.WithLean(1).With(cover: true).Lean, Is.EqualTo(1), "changing another value keeps the lean");
            Assert.Throws<ArgumentOutOfRangeException>(() => input.WithLean(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => input.With(lean: -2));
        }

        [Test]
        public void ADownedPlayerDoesNotLean()
        {
            var downed = new CharacterMotorInput(
                Vector2.Zero, 0f, false, false, false, true, false, false, false, false, false, false, false, 1f, true, 1);

            Assert.That(downed.Restrained().Lean, Is.Zero);
        }

        [Test]
        public void TheSimulationStartsWithoutWaterAndRejectsNullSettings()
        {
            var simulation = NewSimulation();

            Assert.That(simulation.Water, Is.SameAs(NoWaterVolumes.Instance));
            Assert.That(simulation.Water.SurfaceAt(Vector3.Zero), Is.Null);
            Assert.That(simulation.Swim, Is.SameAs(SwimTuning.Default));
            Assert.That(simulation.WallRun, Is.SameAs(WallRunTuning.Default));
            Assert.Throws<ArgumentNullException>(() => simulation.Water = null);
            Assert.Throws<ArgumentNullException>(() => simulation.Swim = null);
            Assert.Throws<ArgumentNullException>(() => simulation.WallRun = null);
        }

        [Test]
        public void ATickCarriesTheUnwrittenStatesThroughUnchanged()
        {
            AddFloor();
            var simulation = NewSimulation();
            var settled = Settle(simulation);
            var lean = new CharacterLeanState(0.5f);
            var swim = new CharacterSwimState(true, 3f, false);
            var wallRun = new CharacterWallRunState(WallRunSide.Left, 0.2f, Vector3.UnitX);
            var state = With(lean, swim, wallRun, 1, settled);

            var next = simulation.Step(state, Input(new Vector2(0f, 1f), lean: 1, jump: true));

            Assert.That(
                next.Lean.Amount,
                Is.EqualTo(0.4f).Within(1e-5f),
                "the stored swim state keeps the body from leaning, so it comes back by LeanSpeed (6/s) for one tick");
            Assert.That(next.Swim, Is.EqualTo(swim));
            Assert.That(next.WallRun, Is.EqualTo(CharacterWallRunState.None), "a body standing on the floor does not run on a wall");
            Assert.That(next.AirJumpsUsed, Is.Zero, "standing and swimming give the air jumps back");
        }

        [Test]
        public void WaterAndTheNewTuningDoNotChangeTheMovementYet()
        {
            AddFloor();
            var plain = NewSimulation();
            var tuned = NewSimulation();
            tuned.Water = new WaterEverywhere(10f);
            tuned.Swim = new SwimTuning(1f, 30f, 2f);
            tuned.WallRun = new WallRunTuning(true, 5f, 0f, 0f, 20f);

            var a = Settle(plain);
            var b = Settle(tuned);
            for (var i = 0; i < 120; i++)
            {
                var input = Input(new Vector2(0.3f, 1f), lean: i % 40 < 20 ? -1 : 1, jump: i % 30 == 0, sprint: i > 60);
                a = plain.Step(a, input);
                b = tuned.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
            }
        }

        [Test]
        public void FallingBelowTheKillPlaneClearsTheNewStates()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = With(
                new CharacterLeanState(1f),
                new CharacterSwimState(true, -55f, true),
                new CharacterWallRunState(WallRunSide.Right, 0.5f, -Vector3.UnitX),
                2,
                new CharacterMotorState(new Vector3(0f, _killPlaneY - 5f, 0f), Vector3.Zero, false, false));

            var next = simulation.Step(state, Input(Vector2.Zero));

            Assert.That(next.Position, Is.EqualTo(_start));
            Assert.That(next.Lean, Is.EqualTo(CharacterLeanState.None));
            Assert.That(next.Swim, Is.EqualTo(CharacterSwimState.None));
            Assert.That(next.WallRun, Is.EqualTo(CharacterWallRunState.None));
            Assert.That(next.AirJumpsUsed, Is.Zero);
        }

        [Test]
        public void TheMotorsAirJumpDoesNothingWithTheDefaults()
        {
            var motor = new PlayerMotor(MotorSpec.Default);
            motor.SetVelocity(new Vector3(1f, -3f, 2f));
            var jumped = 0;
            motor.Jumped += _ => jumped++;

            Assert.That(motor.StepAirJump(0, true, false, new Vector2(1f, 0f), 0f), Is.Zero);
            Assert.That(motor.Velocity, Is.EqualTo(new Vector3(1f, -3f, 2f)));
            Assert.That(jumped, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => motor.StepAirJump(-1, true, false, Vector2.Zero, 0f));
        }

        private static CharacterMotorState With(
            CharacterLeanState lean = default,
            CharacterSwimState swim = default,
            CharacterWallRunState wallRun = default,
            int airJumpsUsed = 0,
            CharacterMotorState? from = null)
        {
            var s = from ?? new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);
            return new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, s.Ladder, s.Knockback, lean, swim, wallRun, airJumpsUsed);
        }

        private static CharacterMotorInput Input(Vector2 move, int lean = 0, bool jump = false, bool sprint = false)
        {
            return new CharacterMotorInput(move, 0f, sprint, jump, false, false, false, false, false, false).WithLean(lean);
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
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

        private void AddFloor()
        {
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity));
        }

        private sealed class WaterEverywhere : IWaterVolumes
        {
            private readonly float _surfaceY;

            public WaterEverywhere(float surfaceY)
            {
                _surfaceY = surfaceY;
            }

            public float? SurfaceAt(Vector3 position)
            {
                return _surfaceY;
            }
        }
    }
}
