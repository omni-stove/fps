using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class JumpGraceTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const float _gravity = 9.81f;
        private const float _jumpHeight = 1.5f;
        private const float _walkSpeed = 4f;
        private const float _airJumpSpeed = 6f;
        private const float _grace = 0.1f;
        private const float _tolerance = 1e-4f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 1f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 _high = new Vector3(0f, 10f, 0f);
        private static readonly LadderSpan _span = new LadderSpan(new Vector3(0f, 0f, 3f), Vector3.UnitZ, 3f, 0.8f);

        private static readonly int[] _groundThenAir = { PlayerMotor.GroundJumpNumber, PlayerMotor.GroundJumpNumber + 1 };

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        private static float GroundJumpSpeed => MathF.Sqrt(2f * _gravity * _jumpHeight);

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            _colliders.Add(WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity));
        }

        [Test]
        public void TheKitDefaultHasNoGrace()
        {
            Assert.That(MotorSpec.DefaultJumpGraceSeconds, Is.Zero);
            Assert.That(MotorSpec.Default.JumpGraceSeconds, Is.Zero);
            Assert.That(Spec(airJumps: 0).JumpGraceSeconds, Is.EqualTo(_grace));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheSpecRejectsABadGrace(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1f, 20f, 0.1f, 2f, jumpGraceSeconds: seconds));
        }

        [TestCase(true, 0.1f, false)]
        [TestCase(false, 0f, false)]
        [TestCase(false, 0.05f, true)]
        public void TheGraceIsOpenOnlyInTheAirWithTimeLeft(bool grounded, float left, bool open)
        {
            Assert.That(JumpGraceStep.IsOpen(grounded, left), Is.EqualTo(open));
        }

        [Test]
        public void AJumpPressClosesTheGrace()
        {
            Assert.That(JumpGraceStep.Step(0.08f, false, true, Spec(airJumps: 0), _tick), Is.Zero);
            Assert.That(JumpGraceStep.Step(0.08f, true, true, Spec(airJumps: 0), _tick), Is.Zero);
        }

        [Test]
        public void TheTickThatStartsOnTheGroundKeepsTheWholeGrace()
        {
            Assert.That(JumpGraceStep.Step(0f, true, false, Spec(airJumps: 0), _tick), Is.EqualTo(_grace));
        }

        [Test]
        public void AnAirTickUsesUpTheGraceAndStopsAtZero()
        {
            Assert.That(JumpGraceStep.Step(0.25f, false, false, Spec(airJumps: 0), 0.1f), Is.EqualTo(0.15f).Within(_tolerance));
            Assert.That(JumpGraceStep.Step(0.05f, false, false, Spec(airJumps: 0), 0.1f), Is.Zero);
        }

        [Test]
        public void TheGraceStepRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => JumpGraceStep.Step(0f, false, false, null, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpGraceStep.Step(-0.1f, false, false, MotorSpec.Default, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpGraceStep.Step(float.NaN, false, false, MotorSpec.Default, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpGraceStep.Step(0f, false, false, MotorSpec.Default, -_tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => JumpGraceStep.Step(0f, false, false, MotorSpec.Default, float.NaN));
        }

        [Test]
        public void APressInTheGraceIsNotAnAirJump()
        {
            var used = AirJumpStep.Step(0, false, true, true, Spec(airJumps: 1), out var jumped);

            Assert.That(jumped, Is.False);
            Assert.That(used, Is.Zero);
        }

        [Test]
        public void TheMotorJumpsLikeOnTheGroundInTheGrace()
        {
            var motor = new PlayerMotor(Spec(airJumps: 0));
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;
            motor.SetVelocity(new Vector3(3f, -1f, 0f));

            motor.Step(new Vector2(0f, 1f), 0f, false, false, true, false, _tick, 1f, true);

            Assert.That(motor.Velocity.X, Is.Zero.Within(_tolerance), "the horizontal speed is the walking speed like a ground jump");
            Assert.That(motor.Velocity.Y, Is.EqualTo(GroundJumpSpeed - (_gravity * _tick)).Within(_tolerance));
            Assert.That(motor.Velocity.Z, Is.EqualTo(_walkSpeed).Within(_tolerance));
            Assert.That(numbers, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber }));
        }

        [Test]
        public void TheMotorFallsThroughAPressOutsideTheGrace()
        {
            var motor = new PlayerMotor(Spec(airJumps: 0));
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;
            motor.SetVelocity(new Vector3(0f, -1f, 0f));

            motor.Step(Vector2.Zero, 0f, false, false, true, false, _tick, 1f, false);

            Assert.That(motor.Velocity.Y, Is.EqualTo(-1f - (_gravity * _tick)).Within(_tolerance));
            Assert.That(numbers, Is.Empty);
        }

        [Test]
        public void TheGraceIsNotOpenWhileGrounded()
        {
            var state = Airborne(_grace);
            var grounded = new CharacterMotorState(
                state.Position, state.Velocity, true, state.Stance, state.Cover, state.Dodge, state.Vault, state.Prone, state.Ladder,
                state.Knockback, state.Lean, state.Swim, state.WallRun, 0, _grace);

            Assert.That(state.IsInJumpGrace, Is.True);
            Assert.That(grounded.IsInJumpGrace, Is.False);
        }

        [Test]
        public void StandingWithTheDefaultSpecLeavesNoGrace()
        {
            var settled = Settle(NewSimulation(MotorSpec.Default));

            Assert.That(settled.JumpGraceLeft, Is.Zero);
        }

        [Test]
        public void StandingFillsTheGrace()
        {
            var settled = Settle(NewSimulation(Spec(airJumps: 0)));

            Assert.That(settled.JumpGraceLeft, Is.EqualTo(_grace));
        }

        [Test]
        public void LeavingTheGroundKeepsTheWholeGraceForTheNextTick()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));
            var onTheEdge = new CharacterMotorState(_high, Vector3.Zero, true, false);

            var off = simulation.Step(onTheEdge, Input());

            Assert.That(off.IsGrounded, Is.False);
            Assert.That(off.JumpGraceLeft, Is.EqualTo(_grace));
        }

        [Test]
        public void AnAirTickInTheSimulationUsesUpTheGrace()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));

            var next = simulation.Step(Airborne(_grace), Input());

            Assert.That(next.JumpGraceLeft, Is.EqualTo(_grace - _tick).Within(_tolerance));
        }

        [Test]
        public void APressInTheGraceIsTheGroundJumpAndIsNotCountedSoTheAirJumpFollows()
        {
            var motor = new PlayerMotor(Spec(airJumps: 1));
            var simulation = NewSimulation(motor);
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;

            var grace = simulation.Step(Airborne(_grace), Input(jump: true));
            Assert.That(grace.AirJumpsUsed, Is.Zero, "the press in the grace is the ground jump");
            Assert.That(grace.Velocity.Y, Is.EqualTo(GroundJumpSpeed - (_gravity * _tick)).Within(_tolerance));
            Assert.That(grace.JumpGraceLeft, Is.Zero, "the grace is used up by the jump");

            var air = simulation.Step(grace, Input(jump: true));
            Assert.That(air.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(air.Velocity.Y, Is.EqualTo(_airJumpSpeed - (_gravity * _tick)).Within(_tolerance));

            var refused = simulation.Step(air, Input(jump: true));
            Assert.That(refused.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(refused.Velocity.Y, Is.EqualTo(air.Velocity.Y - (_gravity * _tick)).Within(_tolerance));
            Assert.That(numbers, Is.EqualTo(_groundThenAir));
        }

        [Test]
        public void ASecondPressAfterTheGraceJumpIsNotAnotherGroundJump()
        {
            var motor = new PlayerMotor(Spec(airJumps: 0));
            var simulation = NewSimulation(motor);
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;

            var grace = simulation.Step(Airborne(_grace), Input(jump: true));
            var again = simulation.Step(grace, Input(jump: true));

            Assert.That(again.Velocity.Y, Is.EqualTo(grace.Velocity.Y - (_gravity * _tick)).Within(_tolerance));
            Assert.That(numbers, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber }));
        }

        [Test]
        public void APressAfterTheGraceRunsOutFallsThrough()
        {
            var motor = new PlayerMotor(Spec(airJumps: 0));
            var simulation = NewSimulation(motor);
            var numbers = new List<int>();
            motor.Jumped += numbers.Add;

            var spent = simulation.Step(Airborne(_tick * 0.5f), Input());
            Assert.That(spent.JumpGraceLeft, Is.Zero);

            var late = simulation.Step(spent, Input(jump: true));

            Assert.That(late.Velocity.Y, Is.EqualTo(spent.Velocity.Y - (_gravity * _tick)).Within(_tolerance));
            Assert.That(numbers, Is.Empty);
        }

        [Test]
        public void APressOutsideTheGraceIsStillAnAirJump()
        {
            var simulation = NewSimulation(Spec(airJumps: 1));

            var next = simulation.Step(Airborne(0f), Input(jump: true));

            Assert.That(next.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(next.Velocity.Y, Is.EqualTo(_airJumpSpeed - (_gravity * _tick)).Within(_tolerance));
        }

        [Test]
        public void ARuleThatForbidsJumpingKeepsTheGraceOpen()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));

            var next = simulation.Step(Airborne(_grace), Input(jump: true).With(jumpAllowed: false));

            Assert.That(next.Velocity.Y, Is.EqualTo(-(_gravity * _tick)).Within(_tolerance));
            Assert.That(next.JumpGraceLeft, Is.EqualTo(_grace - _tick).Within(_tolerance));
        }

        [Test]
        public void ClimbingALadderClosesTheGrace()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));
            var feet = _span.FeetAt(1f, _stance.Radius);
            var state = Airborne(_grace, ladder: CharacterLadderState.Climbing(0, LadderEntry.Front, 1f, false, 0), position: feet);

            var next = simulation.Step(state, Input(ladderAllowed: true));

            Assert.That(next.IsClimbing, Is.True);
            Assert.That(next.JumpGraceLeft, Is.Zero);
        }

        [Test]
        public void SwimmingClosesTheGrace()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));
            simulation.Water = new WaterEverywhere(12f);

            var next = simulation.Step(Airborne(_grace, swim: new CharacterSwimState(true, 12f, true)), Input());

            Assert.That(next.Swim.IsSwimming, Is.True);
            Assert.That(next.JumpGraceLeft, Is.Zero);
        }

        [Test]
        public void BeingKnockedBackClosesTheGrace()
        {
            var simulation = NewSimulation(Spec(airJumps: 0));
            var launched = CharacterMotorSimulation.Launch(Airborne(_grace), new Vector3(0f, 5f, 3f));

            var next = simulation.Step(launched, Input());

            Assert.That(next.IsKnockedBack, Is.True);
            Assert.That(next.JumpGraceLeft, Is.Zero);
        }

        [Test]
        public void TheStateComparesAndChecksTheGrace()
        {
            var plain = Airborne(0f);
            var withGrace = Airborne(_grace);
            var old = new CharacterMotorState(
                plain.Position, plain.Velocity, plain.IsGrounded, plain.Stance, plain.Cover, plain.Dodge, plain.Vault, plain.Prone, plain.Ladder,
                plain.Knockback, plain.Lean, plain.Swim, plain.WallRun, plain.AirJumpsUsed);

            Assert.That(old.JumpGraceLeft, Is.Zero, "the constructor without the grace leaves it closed");
            Assert.That(old, Is.EqualTo(plain));
            Assert.That(withGrace, Is.Not.EqualTo(plain));
            Assert.That(withGrace, Is.EqualTo(Airborne(_grace)));
            Assert.That(withGrace.GetHashCode(), Is.EqualTo(Airborne(_grace).GetHashCode()));
            Assert.That(withGrace.IsFinite, Is.True);
            Assert.That(Airborne(-0.1f).IsFinite, Is.False);
            Assert.That(Airborne(float.NaN).IsFinite, Is.False);
            Assert.That(Airborne(float.PositiveInfinity).IsFinite, Is.False);
        }

        [Test]
        public void KnockingBackKeepsTheGraceInTheState()
        {
            var launched = Airborne(_grace).WithKnockback(CharacterKnockbackState.None.Launch(new Vector3(0f, 5f, 0f)));

            Assert.That(launched.JumpGraceLeft, Is.EqualTo(_grace));
        }

        [Test]
        public void ADifferentGraceAloneDoesNotRewind()
        {
            Assert.That(CharacterMotorState.PositionError(Airborne(0f), Airborne(_grace)), Is.Zero);
        }

        private static MotorSpec Spec(int airJumps)
        {
            return new MotorSpec(
                walkSpeed: _walkSpeed, sprintSpeed: 8f, jumpHeight: _jumpHeight, gravity: _gravity, airControl: 0.5f, crouchSpeed: 2f,
                airJumps: airJumps, airJumpSpeed: _airJumpSpeed, jumpGraceSeconds: _grace);
        }

        private static CharacterMotorState Airborne(
            float jumpGraceLeft,
            CharacterSwimState swim = default,
            CharacterLadderState ladder = default,
            Vector3? position = null)
        {
            var s = new CharacterMotorState(position ?? _high, Vector3.Zero, false, false);
            return new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, ladder, s.Knockback, s.Lean, swim, s.WallRun, 0,
                jumpGraceLeft);
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
