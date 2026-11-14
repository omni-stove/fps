using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CharacterMotorLadderProneTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const float _ledgeHeight = 3f;
        private const float _wallZ = 3f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 1f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);
        private static readonly LadderSpan _span = new LadderSpan(new Vector3(0f, 0f, _wallZ), Vector3.UnitZ, _ledgeHeight, 0.8f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        [Test]
        public void WalkingIntoALadderGrabsItAndClimbingUpLeavesOnTheLedge()
        {
            AddLedge();
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = StepUntil(simulation, state, Input(new Vector2(0f, 1f), ladderAllowed: true), s => s.IsClimbing, 120);
            Assert.That(state.Ladder.Index, Is.EqualTo(0));
            Assert.That(state.Ladder.Entry, Is.EqualTo(LadderEntry.Front));
            var feet = _span.FeetAt(state.Ladder.Height, _stance.Radius);
            Assert.That(state.Position.X, Is.EqualTo(feet.X).Within(1e-4f));
            Assert.That(state.Position.Z, Is.EqualTo(feet.Z).Within(1e-4f), "the grab puts the body against the ladder");

            var climbing = simulation.Step(state, Input(new Vector2(0f, 1f), ladderAllowed: true));
            Assert.That(climbing.Velocity.Y, Is.EqualTo(LadderSpec.DefaultClimbSpeed).Within(1e-4f));
            Assert.That(climbing.Position.Y, Is.GreaterThan(state.Position.Y));

            state = StepUntil(simulation, climbing, Input(new Vector2(0f, 1f), ladderAllowed: true), s => !s.IsClimbing, 180);
            Assert.That(state.Ladder.RegrabRemaining, Is.EqualTo(LadderSpec.DefaultRegrabSeconds));
            Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(state.Position.Z, Is.GreaterThan(_wallZ), "the top exit stands on the ledge");

            state = Settle(simulation, state);
            var floor = Settle(NewSimulation());
            Assert.That(state.Position.Y - floor.Position.Y, Is.EqualTo(_ledgeHeight).Within(0.08f), "standing on the ledge, within the skin");
        }

        [Test]
        public void WalkingOffTheLedgeGrabsTheTopAndForwardClimbsDownUntilTheFloor()
        {
            AddLedge();
            var simulation = NewSimulation();
            var top = new CharacterMotorState(new Vector3(0f, _ledgeHeight + 1.1f, _wallZ + 1.5f), Vector3.Zero, false, false);
            var state = Settle(simulation, top);
            var towardEdge = Input(new Vector2(0f, 1f), yaw: MathF.PI, ladderAllowed: true);

            state = StepUntil(simulation, state, towardEdge, s => s.IsClimbing, 120);
            Assert.That(state.Ladder.Entry, Is.EqualTo(LadderEntry.Top));
            Assert.That(state.Ladder.IsInputLatched, Is.True);

            var lower = simulation.Step(state, towardEdge);
            Assert.That(lower.Velocity.Y, Is.EqualTo(-LadderSpec.DefaultClimbSpeed).Within(1e-4f), "the forward that walked off the edge reads as down");

            state = StepUntil(simulation, lower, towardEdge, s => !s.IsClimbing, 240);
            state = Settle(simulation, state);
            var floor = Settle(NewSimulation());
            Assert.That(state.Position.Y, Is.EqualTo(floor.Position.Y).Within(0.08f), "standing on the floor, within the skin");
            Assert.That(state.Position.Z, Is.LessThan(_wallZ));
        }

        [Test]
        public void JumpingOffTheLadderPushesAwayAndWaitsBeforeTheNextGrab()
        {
            AddLedge();
            var simulation = NewSimulation();
            var state = StepUntil(simulation, Settle(simulation), Input(new Vector2(0f, 1f), ladderAllowed: true), s => s.IsClimbing, 120);
            state = simulation.Step(state, Input(new Vector2(0f, 1f), ladderAllowed: true));

            state = simulation.Step(state, Input(Vector2.Zero, jump: true, ladderAllowed: true));
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.Velocity.Z, Is.EqualTo(-LadderSpec.DefaultJumpOffSpeed).Within(1e-4f));
            Assert.That(state.Ladder.RegrabRemaining, Is.EqualTo(LadderSpec.DefaultRegrabSeconds));

            state = simulation.Step(state, Input(Vector2.Zero, ladderAllowed: true));
            Assert.That(state.Ladder.RegrabRemaining, Is.EqualTo(LadderSpec.DefaultRegrabSeconds - _tick).Within(1e-6f));
        }

        [Test]
        public void WithoutTheLadderRuleTheWallStopsTheBody()
        {
            AddLedge();
            var simulation = NewSimulation();
            var state = Settle(simulation);
            for (var i = 0; i < 120; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f)));
                Assert.That(state.IsClimbing, Is.False);
            }

            Assert.That(state.Position.Z, Is.LessThan(_wallZ - _stance.Radius + 0.01f));
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void TurningTheLadderRuleOffLetsGo()
        {
            AddLedge();
            var simulation = NewSimulation();
            var state = StepUntil(simulation, Settle(simulation), Input(new Vector2(0f, 1f), ladderAllowed: true), s => s.IsClimbing, 120);

            state = simulation.Step(state, Input(new Vector2(0f, 1f)));

            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.Ladder.RegrabRemaining, Is.EqualTo(LadderSpec.DefaultRegrabSeconds));
        }

        [Test]
        public void PressingProneLowersTheBodyAndCrawlsAtTheProneSpeed()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, prone: true, proneAllowed: true));
            Assert.That(state.IsProne, Is.True);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Prone));
            Assert.That(state.Prone.IsActive, Is.True);
            Assert.That(simulation.Body.Current.Height, Is.EqualTo(_stance.Prone.Height));

            var startZ = state.Position.Z;
            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), proneAllowed: true));
            }

            Assert.That(state.IsProne, Is.True);
            Assert.That(state.Position.Z - startZ, Is.EqualTo(ProneSpec.DefaultSpeed).Within(0.02f));

            state = simulation.Step(state, Input(Vector2.Zero, prone: true, proneAllowed: true));
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(state.Prone.IsActive, Is.False);
        }

        [Test]
        public void StartingASprintStandsUpFromProneButHoldingOneDoesNot()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true));
            state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true, prone: true, proneAllowed: true));
            for (var i = 0; i < 60 && state.IsDiving; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true, proneAllowed: true));
            }

            Assert.That(state.IsProne, Is.True, "a sprint held from before the prone is not a new sprint");

            state = simulation.Step(state, Input(new Vector2(0f, 1f), proneAllowed: true));
            state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true, proneAllowed: true));
            Assert.That(state.Prone.IsActive, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing));
        }

        [Test]
        public void ProneUnderALowCeilingStaysDownUntilThereIsRoom()
        {
            // 天井の下面は伏せとしゃがみの間なので、しゃがみにも立ちにも戻れない
            AddBox(new Vector3(0f, 1.1f + 0.5f, 3f), new Vector3(2f, 0.5f, 1f));
            var simulation = NewSimulation();
            var state = simulation.Step(Settle(simulation), Input(Vector2.Zero, prone: true, proneAllowed: true));
            for (var i = 0; i < 150; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), proneAllowed: true));
            }

            Assert.That(state.Position.Z, Is.InRange(2.3f, 3.7f), "the body should be under the ceiling");
            state = simulation.Step(state, Input(Vector2.Zero, prone: true, proneAllowed: true));
            Assert.That(state.Prone.IsActive, Is.False);
            Assert.That(state.IsProne, Is.True, "there is no headroom to crouch or stand");

            state = simulation.Step(state, Input(Vector2.Zero, crouch: true, proneAllowed: true));
            Assert.That(state.IsProne, Is.True);

            for (var i = 0; i < 120; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), proneAllowed: true));
            }

            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing), "past the ceiling the body stands up again");
        }

        [Test]
        public void ProneDoesNotStartInTheAirOrWithoutTheRule()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);

            var refused = simulation.Step(state, Input(Vector2.Zero, prone: true));
            Assert.That(refused.IsProne, Is.False);
            Assert.That(refused.Prone.IsActive, Is.False);

            var jumped = simulation.Step(state, Input(Vector2.Zero, jump: true, proneAllowed: true));
            var midAir = simulation.Step(jumped, Input(Vector2.Zero, prone: true, proneAllowed: true));
            Assert.That(midAir.IsGrounded, Is.False);
            Assert.That(midAir.Prone.IsActive, Is.False);
        }

        [Test]
        public void JumpStandsUpFromProneWithoutJumping()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Settle(simulation), Input(Vector2.Zero, prone: true, proneAllowed: true));
            var groundY = state.Position.Y;

            state = simulation.Step(state, Input(Vector2.Zero, jump: true, proneAllowed: true));

            Assert.That(state.Prone.IsActive, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(groundY).Within(0.01f));
        }

        [Test]
        public void PressingCrouchWhileProneRisesToTheCrouch()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Settle(simulation), Input(Vector2.Zero, crouch: true, proneAllowed: true));
            state = simulation.Step(state, Input(Vector2.Zero, crouch: true, prone: true, proneAllowed: true));
            Assert.That(state.IsProne, Is.True);

            state = simulation.Step(state, Input(Vector2.Zero, crouch: true, proneAllowed: true));
            Assert.That(state.IsProne, Is.True, "a crouch held from before the prone is not a new press");

            state = simulation.Step(state, Input(Vector2.Zero, proneAllowed: true));
            state = simulation.Step(state, Input(Vector2.Zero, crouch: true, proneAllowed: true));
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Crouching));
            Assert.That(state.Prone.IsActive, Is.False);
        }

        [Test]
        public void ProneWhileSprintingDivesTheDiveDistance()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true, proneAllowed: true));
            }

            var startZ = state.Position.Z;
            state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true, prone: true, proneAllowed: true));
            Assert.That(state.IsDiving, Is.True);
            Assert.That(state.IsProne, Is.True);
            Assert.That(state.Prone.DiveDirection.Y, Is.EqualTo(1f).Within(1e-4f));

            var ticks = 1;
            for (; ticks < 120 && state.IsDiving; ticks++)
            {
                state = simulation.Step(state, Input(Vector2.Zero, proneAllowed: true));
            }

            Assert.That(state.IsDiving, Is.False);
            Assert.That(ticks, Is.EqualTo((int)Math.Round(ProneSpec.DefaultDiveSeconds / _tick)));
            Assert.That(state.Position.Z - startZ, Is.EqualTo(ProneSpec.DefaultDiveDistance).Within(0.01f));
            Assert.That(state.IsProne, Is.True);
        }

        [Test]
        public void DownedReleasesProneAndTheLadder()
        {
            AddLedge();
            var simulation = NewSimulation();
            var state = StepUntil(simulation, Settle(simulation), Input(new Vector2(0f, 1f), ladderAllowed: true), s => s.IsClimbing, 120);

            state = simulation.Step(state, Downed());
            Assert.That(state.IsClimbing, Is.False);

            var prone = simulation.Step(Settle(simulation), Input(Vector2.Zero, prone: true, proneAllowed: true));
            var downed = simulation.Step(prone, Downed());
            Assert.That(downed.Prone.IsActive, Is.False);
            Assert.That(downed.Stance, Is.EqualTo(PlayerStance.Crouching));
        }

        [Test]
        public void TheSameInputsGiveTheSameStatesAndReplayingFromAnyStateMatches()
        {
            AddLedge();
            var first = Run(NewSimulation(), Course());
            var second = Run(NewSimulation(), Course());
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Exists(s => s.IsClimbing), Is.True, "the course should climb");
            Assert.That(first.Exists(s => s.IsProne), Is.True, "the course should go prone");
            Assert.That(first.Exists(s => s.IsDiving), Is.True, "the course should dive");

            // 予測の巻き戻しと同じく、途中の状態から別のシミュレーションで続けても同じ答えになる
            var inputs = new List<CharacterMotorInput>(Course());
            foreach (var from in new[] { 40, 130, 200, 260, 330 })
            {
                var replay = NewSimulation();
                var state = first[from];
                for (var i = from + 1; i < inputs.Count; i++)
                {
                    state = replay.Step(state, inputs[i]);
                    Assert.That(state, Is.EqualTo(first[i]), "replay from tick " + from + " differs at tick " + i);
                }
            }
        }

        [Test]
        public void PositionErrorForcesAReplayWhenProneLadderOrDiveDiffer()
        {
            var a = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);
            var prone = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, true, PlayerStance.Prone, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                CharacterProneState.None, CharacterLadderState.None);
            var diving = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, true, PlayerStance.Prone, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                new CharacterProneState(true, true, false, true, Vector2.UnitY, 0.1f), CharacterLadderState.None);
            var climbing = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, false, PlayerStance.Standing, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                CharacterProneState.None, CharacterLadderState.Climbing(0, LadderEntry.Front, 1f, false, 0));
            var otherLadder = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, false, PlayerStance.Standing, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                CharacterProneState.None, CharacterLadderState.Climbing(1, LadderEntry.Front, 1f, false, 0));

            Assert.That(CharacterMotorState.PositionError(a, prone), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(prone, diving), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(a, climbing), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(climbing, otherLadder), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(climbing, climbing), Is.EqualTo(0f));
        }

        [Test]
        public void RulesTurnProneAndLaddersOnAndOff()
        {
            var input = new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, false, false, false, false, false, true, false, false);
            var on = input.WithRules(ActionRules.Default.WithProne(Prone.On).WithLadder(Ladder.On));
            var off = input.WithRules(ActionRules.Default.WithProne(Prone.Off).WithLadder(Ladder.Off));

            Assert.That(on.ProneAllowed, Is.True);
            Assert.That(on.LadderAllowed, Is.True);
            Assert.That(on.Prone, Is.True);
            Assert.That(off.ProneAllowed, Is.False);
            Assert.That(off.LadderAllowed, Is.False);
            Assert.That(input.WithoutCover().Prone, Is.True, "dropping one button keeps the rest");
        }

        private static IEnumerable<CharacterMotorInput> Course()
        {
            for (var i = 0; i < 400; i++)
            {
                if (i < 120)
                {
                    // はしごへ歩いて上り、上の足場へ出る
                    yield return Input(new Vector2(0f, 1f), ladderAllowed: true, proneAllowed: true);
                }
                else if (i < 200)
                {
                    // 足場の上で奥へ走ってから飛び込む
                    yield return Input(new Vector2(0f, 1f), sprint: i < 160, prone: i == 150, ladderAllowed: true, proneAllowed: true);
                }
                else if (i < 260)
                {
                    yield return Input(new Vector2(MathF.Sin(i * 0.1f), 1f), yaw: i * 0.01f, prone: i == 230, ladderAllowed: true, proneAllowed: true);
                }
                else
                {
                    // 足場の縁へ戻ってはしごを下りる
                    yield return Input(new Vector2(0f, 1f), yaw: MathF.PI, jump: i == 390, ladderAllowed: true, proneAllowed: true);
                }
            }
        }

        private static CharacterMotorInput Input(
            Vector2 move,
            float yaw = 0f,
            bool sprint = false,
            bool jump = false,
            bool crouch = false,
            bool prone = false,
            bool proneAllowed = false,
            bool ladderAllowed = false)
        {
            return new CharacterMotorInput(move, yaw, sprint, jump, crouch, false, false, false, false, false, prone, proneAllowed, ladderAllowed);
        }

        private static CharacterMotorInput Downed()
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, true, false, false, false, false, false, true, true);
        }

        private static List<CharacterMotorState> Run(CharacterMotorSimulation simulation, IEnumerable<CharacterMotorInput> inputs)
        {
            var states = new List<CharacterMotorState>();
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            foreach (var input in inputs)
            {
                state = simulation.Step(state, input);
                states.Add(state);
            }

            return states;
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

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            return Settle(simulation, new CharacterMotorState(_start, Vector3.Zero, false, false));
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation, CharacterMotorState state)
        {
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on a floor");
            return state;
        }

        private CharacterMotorSimulation NewSimulation()
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY, ProneSpec.Default)
            {
                SpawnPosition = _start,
                Ladders = new[] { new MapLadder(_span, LadderSpec.Default) },
            };
        }

        // はしごの奥 (面から +Z) に、高さ _ledgeHeight の足場を置く
        private void AddLedge()
        {
            AddBox(new Vector3(0f, _ledgeHeight / 2f, _wallZ + 5f), new Vector3(5f, _ledgeHeight / 2f, 5f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
