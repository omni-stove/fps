using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorSimulationParachuteTests
    {
        private const float _tick = 1f / 60f;

        // 床の上面は y=0。体の原点はカプセルの中心で、足元はそれより 1 m 下
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        [Test]
        public void TheOpenBitOpensItInTheAirWhenTheRuleAllowsAndItIsHighEnough()
        {
            var simulation = NewSimulation();
            var state = Falling(30f);

            var opened = simulation.Step(state, Input(Vector2.Zero, open: true));

            Assert.That(opened.IsParachuting, Is.True);
            Assert.That(simulation.LastParachuteVerdict, Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(simulation.LastParachuteHeight, Is.EqualTo(29f).Within(1e-3f), "measured from the feet, 1 m under the body");
            Assert.That(simulation.LastParachuteClose, Is.Null);
            Assert.That(opened.Velocity.Y, Is.EqualTo(-ParachuteMotion.DefaultDescentSpeed), "it glides from the tick it opened");
        }

        [Test]
        public void ItFallsAtTheDescentSpeedAndSteersTowardTheMoveInput()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Falling(40f), Input(Vector2.Zero, open: true));
            var openedY = state.Position.Y;

            for (var i = 0; i < 120; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f)));
            }

            Assert.That(state.IsParachuting, Is.True);
            Assert.That(openedY - state.Position.Y, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed * 120 * _tick).Within(1e-3f));
            Assert.That(state.Velocity.Y, Is.EqualTo(-ParachuteMotion.DefaultDescentSpeed));

            // 横は 8 m/s² で 6 m/s へ寄せるので、2 秒あれば上限に着いている
            Assert.That(state.Velocity.Z, Is.EqualTo(ParachuteMotion.DefaultHorizontalSpeed).Within(1e-4f));
            Assert.That(state.Velocity.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(state.Parachute.HorizontalVelocity, Is.EqualTo(new Vector2(state.Velocity.X, state.Velocity.Z)));
        }

        [Test]
        public void TheDescentReportedForFallDamageIsCappedAtTheSafeFallSpeed()
        {
            var simulation = NewSimulation(new ParachuteTuning(descentSpeed: 20f));
            var state = simulation.Step(Falling(60f), Input(Vector2.Zero, open: true));
            var before = state.Position.Y;

            state = simulation.Step(state, Input(Vector2.Zero));

            Assert.That(state.Velocity.Y, Is.EqualTo(-MotorSpec.DefaultSafeFallSpeed), "the speed handed to fall damage");
            Assert.That(before - state.Position.Y, Is.EqualTo(20f * _tick).Within(1e-4f), "the body still moves at the tuned speed");
        }

        [Test]
        public void LandingClosesItWithoutFallDamage()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Falling(9f), Input(Vector2.Zero, open: true));
            Assert.That(state.IsParachuting, Is.True);

            var ticks = 0;
            while (state.IsParachuting && ticks < 600)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
                ticks++;
            }

            Assert.That(state.IsParachuting, Is.False);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(simulation.LastParachuteClose, Is.EqualTo(ParachuteCloseReason.Landed));
            // 足元から床まで 8 m を 4 m/s で降りる。開いた tick と体の皮の厚みの分だけ早く着く
            Assert.That(ticks, Is.EqualTo((int)MathF.Ceiling(8f / (ParachuteMotion.DefaultDescentSpeed * _tick))).Within(3));
            Assert.That(FallDamageCalculator.Calculate(-state.Velocity.Y, MotorSpec.Default, 100), Is.Zero);

            state = simulation.Step(state, Input(Vector2.Zero, open: true));
            Assert.That(state.IsParachuting, Is.False, "it does not open on the ground");
            Assert.That(simulation.LastParachuteVerdict, Is.Null, "nor is it even tried");
        }

        [Test]
        public void TheRuleOffDoesNotOpenIt()
        {
            var simulation = NewSimulation();
            var offByRules = Input(Vector2.Zero, open: true, allowed: true).WithRules(ActionRules.Default);

            var refused = simulation.Step(Falling(30f), Input(Vector2.Zero, open: true, allowed: false));
            var refusedByRules = simulation.Step(Falling(30f), offByRules);

            Assert.That(ActionRules.Default.Parachute, Is.EqualTo(Parachute.Off));
            Assert.That(offByRules.ParachuteAllowed, Is.False);
            Assert.That(refused.IsParachuting, Is.False);
            Assert.That(refusedByRules.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteVerdict, Is.Null);
            Assert.That(Input(Vector2.Zero).WithRules(ActionRules.Default.WithParachute(Parachute.On)).ParachuteAllowed, Is.True);
        }

        [Test]
        public void TurningTheRuleOffClosesAnOpenParachute()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Falling(30f), Input(Vector2.Zero, open: true));

            state = simulation.Step(state, Input(Vector2.Zero, allowed: false));

            Assert.That(state.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteClose, Is.EqualTo(ParachuteCloseReason.RuleOff));
        }

        [Test]
        public void TooLowToOpenIsRefused()
        {
            var simulation = NewSimulation();

            var state = simulation.Step(Falling(4f), Input(Vector2.Zero, open: true));

            Assert.That(state.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteVerdict, Is.EqualTo(ParachuteOpenVerdict.TooLow));
            Assert.That(simulation.LastParachuteHeight, Is.EqualTo(3f).Within(1e-3f));
        }

        [Test]
        public void TheCloseBitReleasesIt()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Falling(30f), Input(Vector2.Zero, open: true));

            state = simulation.Step(state, Input(Vector2.Zero, close: true));

            Assert.That(state.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteClose, Is.EqualTo(ParachuteCloseReason.Released));
            Assert.That(state.Velocity.Y, Is.LessThan(-ParachuteMotion.DefaultDescentSpeed), "it falls under gravity again");
        }

        [Test]
        public void BeingThrownByAnExplosionInterruptsIt()
        {
            var simulation = NewSimulation();
            var state = simulation.Step(Falling(30f), Input(Vector2.Zero, open: true));

            state = simulation.Step(CharacterMotorSimulation.Launch(state, new Vector3(3f, 3f, 0f)), Input(Vector2.Zero));

            Assert.That(state.IsParachuting, Is.False);
            Assert.That(state.IsKnockedBack, Is.True);
            Assert.That(simulation.LastParachuteClose, Is.EqualTo(ParachuteCloseReason.Interrupted));
        }

        [Test]
        public void DeployOpensItWithoutAnInputButStillFollowsTheRuleAndTheHeight()
        {
            var simulation = NewSimulation();

            var dropped = simulation.Deploy(Falling(50f), true);
            Assert.That(dropped.IsParachuting, Is.True);
            Assert.That(dropped.Position, Is.EqualTo(Falling(50f).Position), "it moves from the next input");
            Assert.That(simulation.LastParachuteVerdict, Is.EqualTo(ParachuteOpenVerdict.Open));

            var ruleOff = simulation.Deploy(Falling(50f), false);
            Assert.That(ruleOff.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteVerdict, Is.EqualTo(ParachuteOpenVerdict.RuleOff));

            var low = simulation.Deploy(Falling(3f), true);
            Assert.That(low.IsParachuting, Is.False);
            Assert.That(simulation.LastParachuteVerdict, Is.EqualTo(ParachuteOpenVerdict.TooLow));
        }

        [Test]
        public void TheSameInputsGiveTheSameStatesInTwoSimulations()
        {
            var first = NewSimulation();
            var second = NewSimulation();
            var a = Falling(12f);
            var b = Falling(12f);

            for (var i = 0; i < 300; i++)
            {
                var input = Input(new Vector2(MathF.Sin(i * 0.1f), 1f), open: i == 5, yaw: i * 0.01f);
                a = first.Step(a, input);
                b = second.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
            }

            Assert.That(a.IsGrounded, Is.True, "it has landed by the end");
        }

        [Test]
        public void APredictionThatMissedTheParachuteIsCorrected()
        {
            var open = Falling(30f).WithParachute(CharacterParachuteState.Open(Vector2.Zero));

            Assert.That(CharacterMotorState.PositionError(Falling(30f), open), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(open, open), Is.Zero);
            Assert.That(open.IsFinite, Is.True);
            Assert.That(open, Is.Not.EqualTo(Falling(30f)));
        }

        private static CharacterMotorState Falling(float height)
        {
            return new CharacterMotorState(new Vector3(0f, height, 0f), Vector3.Zero, false, false);
        }

        private static CharacterMotorInput Input(Vector2 move, bool open = false, bool close = false, bool allowed = true, float yaw = 0f)
        {
            // ネットの手元と同じく、開くビットは空中で押したジャンプに付けて送る
            return new CharacterMotorInput(
                move, yaw, false, open, false, false, false, false, false, false, false, false, false,
                openParachute: open, closeParachute: close, parachuteAllowed: allowed);
        }

        private CharacterMotorSimulation NewSimulation(ParachuteTuning tuning = null)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f)
            {
                Parachute = tuning ?? ParachuteTuning.Default,
            };
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
