using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CharacterMotorSimulationTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 30;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void WalkingForwardOnTheFloorMovesAtTheWalkSpeed()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var startZ = state.Position.Z;

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f)));
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Z - startZ, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(0.01f));
            Assert.That(state.Position.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(1e-4f));
        }

        [Test]
        public void TheSameInputsGiveTheSameStatesBitForBit()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 4f), new Vector3(2f, 0.4f, 0.3f));
            AddBox(new Vector3(4f, 1.5f, 0f), new Vector3(0.3f, 1.5f, 3f));

            var first = Run(NewSimulation(), Course());
            var second = Run(NewSimulation(), Course());

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first[first.Count - 1].Position, Is.Not.EqualTo(first[0].Position));
        }

        [Test]
        public void CrouchingLowersTheBodyAndALowCeilingKeepsItDown()
        {
            AddFloor();
            AddBox(new Vector3(0f, 1.9f, 3f), new Vector3(2f, 0.2f, 1f));
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, crouch: true));
            Assert.That(state.IsCrouched, Is.True);

            for (var i = 0; i < 70; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), crouch: true));
            }

            Assert.That(state.Position.Z, Is.GreaterThan(2.2f), "the body should be under the low ceiling");
            state = simulation.Step(state, Input(Vector2.Zero));
            Assert.That(state.IsCrouched, Is.True, "there is no headroom to stand under the ceiling");

            for (var i = 0; i < 90; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), crouch: true));
            }

            state = simulation.Step(state, Input(Vector2.Zero));
            Assert.That(state.IsCrouched, Is.False, "past the ceiling the body stands up again");
        }

        [Test]
        public void JumpingLeavesTheGroundAndLandsAgain()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var groundY = state.Position.Y;

            state = simulation.Step(state, Input(Vector2.Zero, jump: true));
            Assert.That(state.IsGrounded, Is.False);

            var peak = state.Position.Y;
            for (var i = 0; i < 90 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
                peak = Math.Max(peak, state.Position.Y);
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(peak - groundY, Is.EqualTo(MotorSpec.DefaultJumpHeight).Within(0.05f));
        }

        [Test]
        public void DodgingRollsTheDodgeDistanceThenWaitsForTheCooldown()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var startX = state.Position.X;

            state = simulation.Step(state, Input(new Vector2(1f, 0f), dodge: true));
            Assert.That(state.IsDodging, Is.True);

            for (var i = 0; i < 60 && state.IsDodging; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsDodging, Is.False);
            Assert.That(state.Position.X - startX, Is.EqualTo(MotorSpec.DefaultDodgeDistance).Within(0.06f));
            Assert.That(state.Dodge.CooldownRemaining, Is.GreaterThan(0f));

            state = simulation.Step(state, Input(new Vector2(1f, 0f), dodge: true));
            Assert.That(state.IsDodging, Is.False, "the cooldown blocks the next roll");
        }

        [Test]
        public void JumpingAtALowWallVaultsOverIt()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 1.2f), new Vector3(2f, 0.4f, 0.2f));
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, jump: true, vaultAllowed: true));

            Assert.That(simulation.LastVaultProbe.CanVault, Is.True, simulation.LastVaultProbe.Verdict.ToString());
            Assert.That(state.IsVaulting, Is.True);
            for (var i = 0; i < 120 && state.IsVaulting; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsVaulting, Is.False);
            Assert.That(state.Position.Z, Is.GreaterThan(1.4f + _stance.Radius), "the body ends past the wall");
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void JumpingWithoutTheVaultRuleIsAPlainJump()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 1.2f), new Vector3(2f, 0.4f, 0.2f));
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, jump: true));

            Assert.That(state.IsVaulting, Is.False);
            Assert.That(state.Velocity.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void PressingCoverAtALowWallEntersLowCoverAndCrouches()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.5f, 1f), new Vector3(2f, 0.5f, 0.2f));
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, cover: true));
            Assert.That(state.Cover.IsInCover, Is.True);
            Assert.That(state.Cover.Height, Is.EqualTo(CoverHeight.Low));
            Assert.That(state.Cover.TopY, Is.EqualTo(1f).Within(1e-3f));

            for (var i = 0; i < 20; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.Cover.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(state.IsCrouched, Is.True);
            // 寄せる目標 (HoldDistance) は skin より壁に近いので、CharacterController と同じく skin の隙間で止まる
            var touching = 0.8f - _stance.Radius - CapsuleMoverSpec.DefaultSkinWidth;
            Assert.That(state.Position.Z, Is.EqualTo(touching).Within(0.005f), "the body slides up to the wall");

            state = simulation.Step(state, Input(Vector2.Zero, cover: true));
            Assert.That(state.Cover.IsInCover, Is.False);
        }

        [Test]
        public void PressingCoverWithNothingAheadStaysOut()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = Settle(simulation);

            state = simulation.Step(state, Input(Vector2.Zero, cover: true));

            Assert.That(state.Cover.IsInCover, Is.False);
        }

        [Test]
        public void DownedInputOnlyKeepsTheYawAndCrouches()
        {
            AddFloor();
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var start = state.Position;

            var downed = new CharacterMotorInput(new Vector2(0f, 1f), 1f, true, true, false, true, false, true, true, true);
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, downed);
            }

            Assert.That(state.IsCrouched, Is.True);
            Assert.That(state.IsDodging, Is.False);
            Assert.That(state.Position.X, Is.EqualTo(start.X).Within(1e-4f));
            Assert.That(state.Position.Z, Is.EqualTo(start.Z).Within(1e-4f));
        }

        [Test]
        public void FallingBelowTheKillPlaneReturnsToTheSpawnPosition()
        {
            var simulation = NewSimulation();
            var spawn = new Vector3(3f, 2f, 1f);
            simulation.SpawnPosition = spawn;
            var state = new CharacterMotorState(new Vector3(0f, _killPlaneY + 0.1f, 0f), new Vector3(0f, -10f, 0f), false, true);

            state = simulation.Step(state, Input(Vector2.Zero));

            Assert.That(state.Position, Is.EqualTo(spawn));
            Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(state.IsCrouched, Is.False);
            Assert.That(simulation.Body.Position, Is.EqualTo(spawn));
        }

        [Test]
        public void RulesDropActionsTheMatchTurnedOff()
        {
            var input = new CharacterMotorInput(Vector2.Zero, 0f, false, true, false, false, true, false, true, false);
            var off = ActionRules.Default.WithCover(Cover.Off).WithDodge(Dodge.Off).WithVault(Vault.Off);
            var on = ActionRules.Default.WithCover(Cover.On).WithDodge(Dodge.On).WithVault(Vault.On);

            var dropped = input.WithRules(off);
            var kept = input.WithRules(on);

            Assert.That(dropped.Cover, Is.False);
            Assert.That(dropped.Dodge, Is.False);
            Assert.That(dropped.VaultAllowed, Is.False);
            Assert.That(kept.Cover, Is.True);
            Assert.That(kept.Dodge, Is.True);
            Assert.That(kept.VaultAllowed, Is.True);
        }

        [Test]
        public void MoveInputLongerThanOneIsClamped()
        {
            var input = new CharacterMotorInput(new Vector2(3f, 4f), 0f, false, false, false, false, false, false, false, false);

            Assert.That(input.Move.X, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(input.Move.Y, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(CharacterMotorInput.IsValid(new Vector2(float.NaN, 0f), 0f), Is.False);
            Assert.That(CharacterMotorInput.IsValid(Vector2.Zero, float.PositiveInfinity), Is.False);
        }

        [Test]
        public void PositionErrorForcesAReplayWhenTheStanceDiffers()
        {
            var a = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);
            var b = new CharacterMotorState(new Vector3(0f, 0f, 0.5f), Vector3.Zero, true, false);
            var crouched = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, true);

            Assert.That(CharacterMotorState.PositionError(a, b), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(CharacterMotorState.PositionError(a, crouched), Is.EqualTo(CharacterMotorState.StanceMismatchError));
        }

        private static CharacterMotorInput Input(
            Vector2 move,
            float yaw = 0f,
            bool sprint = false,
            bool jump = false,
            bool crouch = false,
            bool cover = false,
            bool aim = false,
            bool dodge = false,
            bool vaultAllowed = false)
        {
            return new CharacterMotorInput(move, yaw, sprint, jump, crouch, false, cover, aim, dodge, vaultAllowed);
        }

        private static IEnumerable<CharacterMotorInput> Course()
        {
            for (var i = 0; i < 400; i++)
            {
                var yaw = i * 0.01f;
                var move = new Vector2(MathF.Sin(i * 0.05f), 1f);
                yield return Input(
                    move,
                    yaw,
                    sprint: i % 50 < 20,
                    jump: i % 97 == 0,
                    crouch: i % 120 > 100,
                    cover: i == 150 || i == 200,
                    aim: i % 70 < 10,
                    dodge: i == 260,
                    vaultAllowed: true);
            }
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

        private CharacterMotorSimulation NewSimulation()
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY)
            {
                SpawnPosition = _start,
            };
            return simulation;
        }

        private void AddFloor()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}
