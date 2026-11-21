using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorSimulationSlideTests
    {
        private const float _tick = 1f / 60f;
        private const int _settleTicks = 30;

        // 床の上面は y=0。体の原点はカプセルの中心で、立った足元はそれより 1 m 下
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
        public void CrouchingWhileSprintingStartsASlideAtTheStartSpeed()
        {
            var simulation = NewSimulation();
            var running = Sprint(simulation, Settle(simulation));
            Assert.That(running.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(1e-4f));

            var sliding = simulation.Step(running, Input(sprint: true, crouch: true, slide: true));

            Assert.That(sliding.IsSliding, Is.True);
            Assert.That(sliding.Stance, Is.EqualTo(PlayerStance.Crouching));
            Assert.That(simulation.LastSlideStart.IsSliding, Is.True);
            Assert.That(simulation.LastSlideStart.Speed, Is.EqualTo(SlideMotion.DefaultStartSpeed), "the sprint is slower than the start speed");
            Assert.That(simulation.LastSlideStart.Direction, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.None));

            var afterOneTick = SlideMotion.DefaultStartSpeed - (SlideMotion.DefaultDeceleration * _tick);
            Assert.That(sliding.Slide.Speed, Is.EqualTo(afterOneTick).Within(1e-4f));
            Assert.That(sliding.Velocity.Z, Is.EqualTo(afterOneTick).Within(1e-4f));
            Assert.That(sliding.Velocity.Y, Is.EqualTo(0f).Within(1e-4f), "a flat floor does not tilt the slide");
            Assert.That(
                sliding.Position.Z - running.Position.Z,
                Is.EqualTo((SlideMotion.DefaultStartSpeed + afterOneTick) * 0.5f * _tick).Within(1e-3f));
            Assert.That(sliding.IsGrounded, Is.True);
        }

        [Test]
        public void TheSlideSlowsAndEndsNearTheEndSpeedOnAFlatFloor()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));
            var startZ = state.Position.Z - state.Slide.Distance;
            var ticks = 1;
            var ended = SlideEnd.None;
            var endDistance = 0f;
            while (state.IsSliding && ticks < 300)
            {
                state = simulation.Step(state, Input(crouch: true));
                ticks++;
                if (simulation.LastSlideEnd != SlideEnd.None)
                {
                    ended = simulation.LastSlideEnd;
                    endDistance = simulation.LastSlideEndDistance;
                }
            }

            // 11 m/s から 8 m/s² で 3 m/s まで落ちるのは 1 秒後で、その間に (11 + 3) / 2 = 7 m 進む
            Assert.That(state.IsSliding, Is.False);
            Assert.That(ended, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(ticks, Is.InRange(59, 62));
            Assert.That(endDistance, Is.EqualTo(7f).Within(0.1f));
            Assert.That(state.Position.Z - startZ, Is.EqualTo(7f).Within(0.15f));
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Crouching), "it stays crouched while the crouch is held");
        }

        [Test]
        public void HoldingTheButtonsAfterTheSlideDoesNotSlideAgain()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));
            for (var i = 0; i < 120; i++)
            {
                state = simulation.Step(state, Input(sprint: true, crouch: true, slide: true));
                Assert.That(simulation.LastSlideStart.IsSliding, Is.False, "tick " + i);
            }

            Assert.That(state.IsSliding, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Crouching));
        }

        [Test]
        public void WithTheRuleOffCrouchingWhileSprintingOnlyCrouches()
        {
            var simulation = NewSimulation();
            var running = Sprint(simulation, Settle(simulation));

            var state = simulation.Step(running, Input(sprint: true, crouch: true, slide: true, allowed: false));

            Assert.That(state.IsSliding, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Crouching));
            Assert.That(simulation.LastSlideStart.IsSliding, Is.False);
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultCrouchSpeed).Within(1e-4f));
        }

        [Test]
        public void WithoutTheSlideBitCrouchingWhileSprintingOnlyCrouches()
        {
            var simulation = NewSimulation();

            var state = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true));

            Assert.That(state.IsSliding, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Crouching));
        }

        [Test]
        public void WalkingIsNotFastEnoughToSlide()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            for (var i = 0; i < 10; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(1e-4f));

            state = simulation.Step(state, Input(crouch: true, slide: true));

            Assert.That(state.IsSliding, Is.False);
        }

        [Test]
        public void TheTunedStartSpeedIsUsed()
        {
            var simulation = NewSimulation(new SlideTuning(startSpeed: 14f));

            var state = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));

            Assert.That(simulation.Slide.StartSpeed, Is.EqualTo(14f));
            Assert.That(simulation.LastSlideStart.Speed, Is.EqualTo(14f));
            Assert.That(state.Slide.Speed, Is.EqualTo(14f - (SlideMotion.DefaultDeceleration * _tick)).Within(1e-4f));
        }

        [Test]
        public void TurningTheRuleOffCancelsTheSlide()
        {
            var simulation = NewSimulation();
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));

            var state = simulation.Step(sliding, Input(crouch: true, allowed: false));

            Assert.That(state.IsSliding, Is.False);
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.Cancelled));
            Assert.That(simulation.LastSlideEndSpeed, Is.EqualTo(sliding.Slide.Speed));
        }

        [Test]
        public void GoingDownCancelsTheSlide()
        {
            var simulation = NewSimulation();
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));
            var downed = new CharacterMotorInput(
                new Vector2(0f, 1f), 0f, true, false, true, true, false, false, false, false, false, false, false,
                slide: true, slideAllowed: true);

            var state = simulation.Step(sliding, downed);

            Assert.That(state.IsSliding, Is.False);
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.Cancelled));
        }

        [Test]
        public void JumpingEndsTheSlideAndKeepsItsSpeed()
        {
            var simulation = NewSimulation();
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));

            var state = simulation.Step(sliding, Input(crouch: true, jump: true));

            var launch = MathF.Sqrt(2f * MotorSpec.DefaultGravity * MotorSpec.DefaultJumpHeight);
            Assert.That(state.IsSliding, Is.False);
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(state.Velocity.Z, Is.EqualTo(sliding.Slide.Speed).Within(1e-4f));
            Assert.That(state.Velocity.Y, Is.EqualTo(launch - (MotorSpec.DefaultGravity * _tick)).Within(1e-3f));
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing), "it stands up for the jump like SlideMovement");
        }

        [Test]
        public void WithJumpOffTheJumpDoesNotEndTheSlide()
        {
            var simulation = NewSimulation();
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));

            var state = simulation.Step(sliding, Input(crouch: true, jump: true).With(jumpAllowed: false));

            Assert.That(state.IsSliding, Is.True);
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void AWallAheadStopsTheSlide()
        {
            // 走り出す前の位置から 3 m 先に、体の前面で止まる厚い壁を置く
            AddBox(new Vector3(0f, 1f, 3.5f), new Vector3(5f, 2f, 0.5f));
            var simulation = NewSimulation();
            var state = simulation.Step(Sprint(simulation, Settle(simulation)), Input(sprint: true, crouch: true, slide: true));
            var ended = SlideEnd.None;
            for (var i = 0; i < 60 && state.IsSliding; i++)
            {
                state = simulation.Step(state, Input(crouch: true));
                if (simulation.LastSlideEnd != SlideEnd.None)
                {
                    ended = simulation.LastSlideEnd;
                }
            }

            Assert.That(state.IsSliding, Is.False, "the slide should not keep pushing into the wall");
            Assert.That(ended, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(state.Position.Z, Is.LessThan(3f - 0.5f + 0.01f), "the body stops at the face of the wall");
        }

        [Test]
        public void SlidingOffALedgeEndsAfterTheGrace()
        {
            _colliders.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(5f, 1f, 1.5f));
            AddBox(new Vector3(0f, -21f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation();
            var state = simulation.Step(Sprint(simulation, Settle(simulation), 1), Input(sprint: true, crouch: true, slide: true));
            var ended = SlideEnd.None;
            for (var i = 0; i < 60 && state.IsSliding; i++)
            {
                state = simulation.Step(state, Input(crouch: true));
                if (simulation.LastSlideEnd != SlideEnd.None)
                {
                    ended = simulation.LastSlideEnd;
                }
            }

            Assert.That(ended, Is.EqualTo(SlideEnd.Airborne));
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.Velocity.Y, Is.LessThan(0f), "it falls once it leaves the ledge");
        }

        [Test]
        public void TheSameInputsGiveTheSameStatesInTwoSimulations()
        {
            var first = NewSimulation();
            var second = NewSimulation();
            var a = new CharacterMotorState(_start, Vector3.Zero, false, false);
            var b = a;
            for (var i = 0; i < 300; i++)
            {
                var crouch = i % 100 >= 40 && i % 100 < 90;
                var input = Input(sprint: i % 100 < 50, crouch: crouch, slide: crouch, jump: i % 100 == 60);
                a = first.Step(a, input);
                b = second.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
                Assert.That(a.IsFinite, Is.True, "tick " + i);
            }
        }

        [Test]
        public void APredictionThatMissedTheSlideIsCorrected()
        {
            var still = new CharacterMotorState(_start, Vector3.Zero, true, true);
            var sliding = still.WithSlide(CharacterSlideState.Sliding(new Vector2(0f, 2f), 9f, 0.2f, 2f, 0f));

            Assert.That(CharacterMotorState.PositionError(still, sliding), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(sliding, sliding), Is.Zero);
            Assert.That(sliding.IsFinite, Is.True);
            Assert.That(sliding, Is.Not.EqualTo(still));
            Assert.That(sliding.Slide.Direction, Is.EqualTo(new Vector2(0f, 1f)), "the direction is kept normalized");
            Assert.That(sliding.WithKnockback(CharacterKnockbackState.None).Slide, Is.EqualTo(sliding.Slide));
            Assert.That(sliding.WithParachute(CharacterParachuteState.None).Slide, Is.EqualTo(sliding.Slide));
        }

        [Test]
        public void TheSlideStateRejectsBadValues()
        {
            Assert.Throws<ArgumentException>(() => CharacterSlideState.Sliding(Vector2.Zero, 1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentException>(() => CharacterSlideState.Sliding(new Vector2(float.NaN, 1f), 1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterSlideState.Sliding(Vector2.UnitY, -1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterSlideState.Sliding(Vector2.UnitY, 1f, float.PositiveInfinity, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterSlideState.Sliding(Vector2.UnitY, 1f, 0f, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterSlideState.Sliding(Vector2.UnitY, 1f, 0f, 0f, float.NaN));
            Assert.That(CharacterSlideState.None.IsValid, Is.True);
            Assert.Throws<ArgumentNullException>(() => NewSimulation().Slide = null);
        }

        [Test]
        public void RulesAndRestrainingSetWhetherItMaySlide()
        {
            var on = Input(slide: true, allowed: false).WithRules(ActionRules.Default.WithSlide(Slide.On));
            var off = Input(slide: true, allowed: true).WithRules(ActionRules.Default.WithSlide(Slide.Off));
            var downed = new CharacterMotorInput(
                Vector2.Zero, 0f, true, false, true, true, false, false, false, false, false, false, false,
                slide: true, slideAllowed: true).Restrained();

            Assert.That(on.SlideAllowed, Is.True);
            Assert.That(on.Slide, Is.True);
            Assert.That(off.SlideAllowed, Is.False);
            Assert.That(downed.Slide, Is.False);
            Assert.That(downed.SlideAllowed, Is.False);
            Assert.That(default(CharacterMotorInput).SlideAllowed, Is.False);
        }

        private static CharacterMotorState Sprint(CharacterMotorSimulation simulation, CharacterMotorState state, int ticks = 5)
        {
            for (var i = 0; i < ticks; i++)
            {
                state = simulation.Step(state, Input(sprint: true));
            }

            return state;
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

        private static CharacterMotorInput Input(
            bool sprint = false, bool crouch = false, bool slide = false, bool jump = false, bool allowed = true)
        {
            return Input(new Vector2(0f, 1f), sprint, crouch, slide, jump, allowed);
        }

        private static CharacterMotorInput Input(
            Vector2 move, bool sprint = false, bool crouch = false, bool slide = false, bool jump = false, bool allowed = true)
        {
            return new CharacterMotorInput(
                move, 0f, sprint, jump, crouch, false, false, false, false, false, false, false, false,
                slide: slide, slideAllowed: allowed);
        }

        private CharacterMotorSimulation NewSimulation(SlideTuning tuning = null)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f)
            {
                SpawnPosition = _start,
                Slide = tuning ?? SlideTuning.Default,
            };
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
