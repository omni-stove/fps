using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AirJumpTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const float _gravity = 9.81f;
        private const float _jumpHeight = 1.5f;
        private const float _airJumpSpeed = 6f;
        private const float _tolerance = 1e-4f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 1f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 _high = new Vector3(0f, 10f, 0f);
        private static readonly LadderSpan _span = new LadderSpan(new Vector3(0f, 0f, 3f), Vector3.UnitZ, 3f, 0.8f);

        private static readonly int[] _twoThree = { 2, 3 };
        private static readonly int[] _oneTwo = { 1, 2 };
        private static readonly int[] _oneTwoOne = { 1, 2, 1 };

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            _colliders.Add(WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity));
        }

        [Test]
        public void TheDefaultSpecNeverJumpsInTheAir()
        {
            var used = AirJumpStep.Step(0, false, true, MotorSpec.Default, out var jumped);

            Assert.That(MotorSpec.Default.AirJumps, Is.Zero);
            Assert.That(jumped, Is.False);
            Assert.That(used, Is.Zero);
        }

        [Test]
        public void OneAirJumpIsUsedOnceThenRefused()
        {
            var spec = Spec(airJumps: 1);

            var first = AirJumpStep.Step(0, false, true, spec, out var firstJumped);
            var second = AirJumpStep.Step(first, false, true, spec, out var secondJumped);

            Assert.That(firstJumped, Is.True);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(secondJumped, Is.False);
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void HoldingOffTheJumpKeepsTheCount()
        {
            var used = AirJumpStep.Step(1, false, false, Spec(airJumps: 2), out var jumped);

            Assert.That(jumped, Is.False);
            Assert.That(used, Is.EqualTo(1));
        }

        [Test]
        public void TouchingTheGroundResetsWithoutCounting()
        {
            var used = AirJumpStep.Step(2, true, true, Spec(airJumps: 2), out var jumped);

            Assert.That(jumped, Is.False, "a press on the ground is the ground jump");
            Assert.That(used, Is.Zero);
        }

        [Test]
        public void TheStepRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AirJumpStep.Step(0, false, true, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirJumpStep.Step(-1, false, true, MotorSpec.Default, out _));
        }

        [Test]
        public void TheMotorJumpsAtTheAirJumpSpeedAndTellsWhichJumpItIs()
        {
            var motor = new PlayerMotor(Spec(airJumps: 2));
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;
            motor.SetVelocity(new Vector3(0f, -4f, 0f));

            var first = motor.StepAirJump(0, true, false, Vector2.Zero, 0f);
            Assert.That(motor.Velocity.Y, Is.EqualTo(_airJumpSpeed));
            motor.SetVelocity(new Vector3(0f, -1f, 0f));
            var second = motor.StepAirJump(first, true, false, Vector2.Zero, 0f);
            Assert.That(motor.Velocity.Y, Is.EqualTo(_airJumpSpeed));
            motor.SetVelocity(new Vector3(0f, -1f, 0f));
            var third = motor.StepAirJump(second, true, false, Vector2.Zero, 0f);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(2));
            Assert.That(third, Is.EqualTo(2));
            Assert.That(motor.Velocity.Y, Is.EqualTo(-1f), "the third press finds no air jumps left");
            Assert.That(numbers, Is.EqualTo(_twoThree), "the ground jump is 1, so the first air jump is 2");
        }

        [Test]
        public void TheGroundJumpIsNumberOne()
        {
            var motor = new PlayerMotor(Spec(airJumps: 1));
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;

            var used = motor.StepAirJump(0, true, true, Vector2.Zero, 0f);
            motor.Step(Vector2.Zero, 0f, false, false, true, true, _tick);

            Assert.That(used, Is.Zero);
            Assert.That(numbers, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber }));
            Assert.That(PlayerMotor.GroundJumpNumber, Is.EqualTo(1));
        }

        [TestCase(1f, 0f, 0f, 5f, 0f)]
        [TestCase(0f, -1f, 0f, 0f, -5f)]
        [TestCase(0f, 1f, MathF.PI / 2f, 5f, 0f)]
        [TestCase(0f, 0f, 0f, 3f, 4f)]
        [TestCase(0.005f, 0f, 0f, 3f, 4f)]
        public void TheAirJumpTurnsTheHorizontalSpeedToTheInput(float x, float y, float yaw, float expectedX, float expectedZ)
        {
            var motor = new PlayerMotor(Spec(airJumps: 1));
            motor.SetVelocity(new Vector3(3f, -2f, 4f));

            motor.StepAirJump(0, true, false, new Vector2(x, y), yaw);

            Assert.That(motor.Velocity.X, Is.EqualTo(expectedX).Within(_tolerance));
            Assert.That(motor.Velocity.Y, Is.EqualTo(_airJumpSpeed));
            Assert.That(motor.Velocity.Z, Is.EqualTo(expectedZ).Within(_tolerance));
        }

        [Test]
        public void TheMotorRejectsNonFiniteInput()
        {
            var motor = new PlayerMotor(Spec(airJumps: 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => motor.StepAirJump(0, true, false, new Vector2(float.NaN, 0f), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motor.StepAirJump(0, true, false, Vector2.Zero, float.PositiveInfinity));
        }

        [Test]
        public void TheSimulationWithTheDefaultSpecFallsThroughAnAirPress()
        {
            var simulation = NewSimulation(MotorSpec.Default);
            var falling = new CharacterMotorState(_high, Vector3.Zero, false, false);

            var next = simulation.Step(falling, Input(jump: true));

            Assert.That(next.AirJumpsUsed, Is.Zero);
            Assert.That(next.Velocity.Y, Is.EqualTo(-MotorSpec.Default.Gravity * _tick).Within(_tolerance));
        }

        [Test]
        public void TheGroundJumpIsNotCountedSoOneAirJumpFollowsItThenLandingGivesItBack()
        {
            var motor = new PlayerMotor(Spec(airJumps: 1));
            var simulation = NewSimulation(motor);
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;
            var grounded = Settle(simulation);

            var ground = simulation.Step(grounded, Input(jump: true));
            Assert.That(ground.IsGrounded, Is.False);
            Assert.That(ground.AirJumpsUsed, Is.Zero, "the tick still on the ground is the ground jump");
            Assert.That(ground.Velocity.Y, Is.EqualTo(MathF.Sqrt(2f * _gravity * _jumpHeight) - (_gravity * _tick)).Within(_tolerance));

            var air = simulation.Step(ground, Input(jump: true));
            Assert.That(air.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(air.Velocity.Y, Is.EqualTo(_airJumpSpeed - (_gravity * _tick)).Within(_tolerance));

            var refused = simulation.Step(air, Input(jump: true));
            Assert.That(refused.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(refused.Velocity.Y, Is.EqualTo(air.Velocity.Y - (_gravity * _tick)).Within(_tolerance));
            Assert.That(numbers, Is.EqualTo(_oneTwo));

            var landed = StepUntil(simulation, refused, Input(), s => s.IsGrounded, 240);
            Assert.That(landed.AirJumpsUsed, Is.Zero);

            simulation.Step(landed, Input(jump: true));
            Assert.That(numbers, Is.EqualTo(_oneTwoOne));
        }

        [Test]
        public void FallingWithoutAGroundJumpCanStillUseTheAirJump()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));
            var falling = new CharacterMotorState(_high, new Vector3(0f, -3f, 0f), false, false);

            var next = simulation.Step(falling, Input(jump: true));

            Assert.That(next.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(next.Velocity.Y, Is.EqualTo(_airJumpSpeed - (_gravity * _tick)).Within(_tolerance));
        }

        [Test]
        public void SwimmingInTheAirGivesTheAirJumpsBack()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));
            simulation.Water = new WaterEverywhere(12f);
            var state = Airborne(swim: new CharacterSwimState(true, 12f, true));

            var next = simulation.Step(state, Input());

            Assert.That(next.IsGrounded, Is.False);
            Assert.That(next.AirJumpsUsed, Is.Zero);
        }

        [Test]
        public void AWallRunWithNoWallEndsWithoutGivingTheAirJumpsBack()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));
            var state = Airborne(wallRun: new CharacterWallRunState(WallRunSide.Left, 0.1f, Vector3.UnitX));

            var next = simulation.Step(state, Input());

            Assert.That(next.IsGrounded, Is.False);
            Assert.That(next.WallRun.IsRunning, Is.False);
            Assert.That(next.AirJumpsUsed, Is.EqualTo(1), "only a tick that is still on the wall gives them back (CharacterWallRunTests)");
        }

        [Test]
        public void ClimbingALadderGivesTheAirJumpsBack()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));
            var feet = _span.FeetAt(1f, _stance.Radius);
            var state = Airborne(ladder: CharacterLadderState.Climbing(0, LadderEntry.Front, 1f, false, 0), position: feet);

            var next = simulation.Step(state, Input(ladderAllowed: true));

            Assert.That(next.IsClimbing, Is.True);
            Assert.That(next.AirJumpsUsed, Is.Zero);
        }

        [Test]
        public void StayingInTheAirKeepsTheCount()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));

            var next = simulation.Step(Airborne(), Input());

            Assert.That(next.IsGrounded, Is.False);
            Assert.That(next.AirJumpsUsed, Is.EqualTo(1));
        }

        private static MotorSpec Spec(int airJumps)
        {
            return new MotorSpec(
                walkSpeed: 4f, sprintSpeed: 8f, jumpHeight: _jumpHeight, gravity: _gravity, airControl: 0.5f, crouchSpeed: 2f,
                airJumps: airJumps, airJumpSpeed: _airJumpSpeed);
        }

        // 着地してから 1 回跳んだ、宙にいる状態
        private static CharacterMotorState Airborne(
            CharacterSwimState swim = default,
            CharacterWallRunState wallRun = default,
            CharacterLadderState ladder = default,
            Vector3? position = null)
        {
            var s = new CharacterMotorState(position ?? _high, Vector3.Zero, false, false);
            return new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, ladder, s.Knockback, s.Lean, swim, wallRun, 1);
        }

        private static CharacterMotorInput Input(bool jump = false, bool ladderAllowed = false)
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, jump, false, false, false, false, false, false, false, false, ladderAllowed);
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private static CharacterMotorState StepUntil(
            CharacterMotorSimulation simulation, CharacterMotorState state, CharacterMotorInput input, Func<CharacterMotorState, bool> done, int limit)
        {
            for (var i = 0; i < limit; i++)
            {
                state = simulation.Step(state, input);
                if (done(state))
                {
                    return state;
                }
            }

            Assert.Fail("the condition did not hold within " + limit + " ticks; ended at " + state.Position);
            return state;
        }

        private CharacterMotorSimulation NewSimulation(MotorSpec spec)
        {
            return NewSimulation(new PlayerMotor(spec));
        }

        private CharacterMotorSimulation NewSimulation(PlayerMotor motor)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, motor, _tick, _killPlaneY, ProneSpec.Default)
            {
                SpawnPosition = _start,
                Ladders = new[] { new MapLadder(_span, LadderSpec.Default) },
            };
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
