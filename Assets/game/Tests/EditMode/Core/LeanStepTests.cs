using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LeanStepTests
    {
        // 2 進で割り切れる刻みと速さにして、傾きを足し合わせても誤差が出ないようにする
        private const float _tick = 0.0625f;
        private const float _tolerance = 1e-4f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private static readonly MotorSpec _spec = new MotorSpec(
            MotorSpec.DefaultWalkSpeed,
            MotorSpec.DefaultSprintSpeed,
            MotorSpec.DefaultJumpHeight,
            MotorSpec.DefaultGravity,
            MotorSpec.DefaultAirControl,
            MotorSpec.DefaultCrouchSpeed,
            leanSideOffset: 0.4f,
            leanSpeed: 4f);

        private static readonly float[] _leanRight = { 0.25f, 0.5f, 0.75f, 1f, 1f };
        private static readonly float[] _releaseFromRight = { 0.75f, 0.5f, 0.25f, 0f, 0f };
        private static readonly float[] _noRoom = { 0f, 0f };

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void HoldingLeansAtTheConfiguredSpeedUpToTheFullLean()
        {
            var body = NewBody();

            Assert.That(Run(body, 0f, 1, 5), Is.EqualTo(_leanRight));
            Assert.That(Run(body, 0f, -1, 5), Is.EqualTo(new[] { -0.25f, -0.5f, -0.75f, -1f, -1f }));
        }

        [Test]
        public void TheSpeedComesFromTheSpec()
        {
            var body = NewBody();
            var slow = new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                leanSpeed: 2f);

            var next = LeanStep.Step(State(0f), Input(1), CharacterSwimState.None, body, slow, _tick);

            Assert.That(next.Amount, Is.EqualTo(0.125f));
        }

        [Test]
        public void ReleasingComesBackAtTheSameSpeed()
        {
            var body = NewBody();

            Assert.That(Run(body, 1f, 0, 5), Is.EqualTo(_releaseFromRight));
            Assert.That(Run(body, -0.5f, 0, 3), Is.EqualTo(new[] { -0.25f, 0f, 0f }));
        }

        [Test]
        public void SwitchingSidesPassesThroughUpright()
        {
            var body = NewBody();

            Assert.That(Run(body, 0.5f, -1, 4), Is.EqualTo(new[] { 0.25f, 0f, -0.25f, -0.5f }));
        }

        [Test]
        public void InCoverTheBodyDoesNotLeanAndComesBack()
        {
            var body = NewBody();
            var cover = new CharacterCoverState(
                new CoverSnapshot(CoverPhase.In, CoverHeight.Low, Vector3.UnitZ, 0f, 1f, 0f, 0f), 1f, CoverEdge.None, 0f);

            var still = LeanStep.Step(State(0f, cover: cover), Input(1), CharacterSwimState.None, body, _spec, _tick);
            var back = LeanStep.Step(State(0.5f, cover: cover), Input(1), CharacterSwimState.None, body, _spec, _tick);

            Assert.That(still.Amount, Is.Zero);
            Assert.That(back.Amount, Is.EqualTo(0.25f));
        }

        [Test]
        public void DashingProneClimbingAndSwimmingKeepTheBodyUpright()
        {
            var body = NewBody();
            var forward = new Vector2(0f, 1f);

            Assert.That(Lean(body, State(0f), Input(1, forward, sprint: true)), Is.Zero, "dashing");
            Assert.That(Lean(body, State(0f, stance: PlayerStance.Prone), Input(1)), Is.Zero, "prone");
            Assert.That(
                Lean(body, State(0f, prone: new CharacterProneState(true, false, false, false, Vector2.Zero, 0f)), Input(1)),
                Is.Zero,
                "going prone");
            Assert.That(
                Lean(body, State(0f, ladder: CharacterLadderState.Climbing(0, LadderEntry.Front, 1f, false, 0)), Input(1)), Is.Zero, "on a ladder");
            Assert.That(
                LeanStep.Step(State(0f), Input(1), new CharacterSwimState(true, 2f, false), body, _spec, _tick).Amount, Is.Zero, "swimming");
        }

        [Test]
        public void SprintWithoutDashingStillLeans()
        {
            var body = NewBody();

            Assert.That(Lean(body, State(0f), Input(1, Vector2.Zero, sprint: true)), Is.EqualTo(0.25f), "standing still");
            Assert.That(Lean(body, State(0f, stance: PlayerStance.Crouching), Input(1, new Vector2(0f, 1f), sprint: true)), Is.EqualTo(0.25f), "crouched");
            Assert.That(Lean(body, State(0f), Input(1, new Vector2(0f, 1f))), Is.EqualTo(0.25f), "walking");
        }

        [Test]
        public void AWallStopsTheLeanWhereTheBodyFits()
        {
            AddWallOnTheRight(0.78f);
            var body = NewBody();

            var leaned = Run(body, 0f, 1, 3);

            Assert.That(leaned[0], Is.EqualTo(0.25f).Within(_tolerance));
            Assert.That(leaned[1], Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(
                leaned[2],
                Is.EqualTo(0.5f).Within(_tolerance),
                "the body (radius 0.5) touches the wall after 0.28, and 0.28 less the 0.08 skin is half of the 0.4 side offset");
            Assert.That(Run(body, 0f, -1, 1)[0], Is.EqualTo(-0.25f), "the other side is open");
        }

        [Test]
        public void ALeanAlreadyInTheWallIsPulledBackAtOnce()
        {
            AddWallOnTheRight(0.78f);
            var body = NewBody();

            Assert.That(Run(body, 1f, 1, 1)[0], Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(Run(body, 1f, 0, 1)[0], Is.EqualTo(0.5f).Within(_tolerance), "coming back by speed would still be in the wall");
            Assert.That(Run(body, 1f, -1, 1)[0], Is.EqualTo(0.5f).Within(_tolerance), "switching sides is checked against the side it still leans to");
        }

        [Test]
        public void AWallTouchingTheBodyLeavesNoRoom()
        {
            AddWallOnTheRight(0.55f);
            var body = NewBody();

            Assert.That(Run(body, 0f, 1, 2), Is.EqualTo(_noRoom));
        }

        [Test]
        public void RoomIsTheShareOfTheSideOffsetThatFits()
        {
            AddWallOnTheRight(0.78f);
            var body = NewBody();

            Assert.That(LeanStep.Room(body, Vector3.Zero, 0f, 1, _spec), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(LeanStep.Room(body, Vector3.Zero, 0f, -1, _spec), Is.EqualTo(1f));
            Assert.That(LeanStep.Room(body, Vector3.Zero, MathF.PI, -1, _spec), Is.EqualTo(0.5f).Within(_tolerance), "turned around, the wall is on the left");
            Assert.That(LeanStep.Room(body, new Vector3(-1f, 0f, 0f), 0f, 1, _spec), Is.EqualTo(1f), "far enough from the wall");
            Assert.Throws<ArgumentOutOfRangeException>(() => LeanStep.Room(body, Vector3.Zero, 0f, 0, _spec));
        }

        [Test]
        public void TheEyeMovesSidewaysByTheSideOffset()
        {
            Assert.That(LeanStep.EyeOffset(1f, 0f, _spec), Is.EqualTo(new Vector3(0.4f, 0f, 0f)));
            Assert.That(LeanStep.EyeOffset(-0.5f, 0f, _spec), Is.EqualTo(new Vector3(-0.2f, 0f, 0f)));
            Assert.That(LeanStep.EyeOffset(0f, 0f, _spec), Is.EqualTo(Vector3.Zero));
            Assert.That(LeanStep.EyeOffset(new CharacterLeanState(0.25f), 0f, _spec), Is.EqualTo(new Vector3(0.1f, 0f, 0f)));

            var turned = LeanStep.EyeOffset(1f, MathF.PI * 0.5f, _spec);
            Assert.That(turned.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(turned.Y, Is.Zero);
            Assert.That(turned.Z, Is.EqualTo(-0.4f).Within(1e-6f), "facing +X, the right is -Z");
        }

        [Test]
        public void TheEyeOffsetIgnoresBrokenAmounts()
        {
            Assert.That(LeanStep.EyeOffset(2f, 0f, _spec), Is.EqualTo(new Vector3(0.4f, 0f, 0f)));
            Assert.That(LeanStep.EyeOffset(float.NaN, 0f, _spec), Is.EqualTo(Vector3.Zero));
            Assert.Throws<ArgumentNullException>(() => LeanStep.EyeOffset(1f, 0f, null));
        }

        [Test]
        public void TowardRejectsABrokenTick()
        {
            Assert.That(LeanStep.Toward(0.5f, 1f, _spec, 0f), Is.EqualTo(0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => LeanStep.Toward(0f, 1f, _spec, -_tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => LeanStep.Toward(0f, 1f, _spec, float.NaN));
        }

        [Test]
        public void TheSimulationLeansThroughTheStep()
        {
            var body = NewBody();
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(_spec), _tick, -50f);
            var state = State(0f);

            state = simulation.Step(state, Input(1));
            Assert.That(state.Lean.Amount, Is.EqualTo(0.25f));
            state = simulation.Step(state, Input(1));
            Assert.That(state.Lean.Amount, Is.EqualTo(0.5f));
            state = simulation.Step(state, Input(0));
            Assert.That(state.Lean.Amount, Is.EqualTo(0.25f));
        }

        private static float[] Run(CharacterBody body, float from, int lean, int ticks)
        {
            var amounts = new float[ticks];
            var state = State(from);
            for (var i = 0; i < ticks; i++)
            {
                var next = LeanStep.Step(state, Input(lean), CharacterSwimState.None, body, _spec, _tick);
                amounts[i] = next.Amount;
                state = State(next.Amount);
            }

            return amounts;
        }

        private static float Lean(CharacterBody body, CharacterMotorState state, CharacterMotorInput input)
        {
            return LeanStep.Step(state, input, CharacterSwimState.None, body, _spec, _tick).Amount;
        }

        private static CharacterMotorState State(
            float lean,
            PlayerStance stance = PlayerStance.Standing,
            CharacterCoverState cover = default,
            CharacterProneState prone = default,
            CharacterLadderState ladder = default)
        {
            return new CharacterMotorState(
                Vector3.Zero,
                Vector3.Zero,
                false,
                stance,
                cover,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                prone,
                ladder,
                CharacterKnockbackState.None,
                new CharacterLeanState(lean),
                CharacterSwimState.None,
                CharacterWallRunState.None,
                0);
        }

        private static CharacterMotorInput Input(int lean, Vector2 move = default, bool sprint = false)
        {
            return new CharacterMotorInput(move, 0f, sprint, false, false, false, false, false, false, false).WithLean(lean);
        }

        private CharacterBody NewBody()
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            body.Place(Vector3.Zero, false);
            return body;
        }

        private void AddWallOnTheRight(float faceX)
        {
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1, 0, WorldCollider.StaticGroup, new Vector3(faceX + 0.5f, 0.5f, 0f), new Vector3(0.5f, 2f, 2f), Quaternion.Identity));
        }
    }
}
