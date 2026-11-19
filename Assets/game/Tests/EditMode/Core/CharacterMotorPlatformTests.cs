using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 床の板は上面が y = 0 から始まる。体は高さ 2・中心 0 なので、立つと本体は板の上面から 1 (と skin) 上に来る。
    public sealed class CharacterMotorPlatformTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 30;
        private const float _halfThickness = 0.25f;
        private const float _halfWidth = 4f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<WorldCollider> _statics = new List<WorldCollider>();
        private SwappableWorld _world;
        private Vector3 _platformTop;

        [SetUp]
        public void SetUp()
        {
            _statics.Clear();
            _world = new SwappableWorld();
            _platformTop = Vector3.Zero;
        }

        [Test]
        public void ADefaultInputCarriesNoPlatform()
        {
            Assert.That(default(CharacterMotorInput).Platform.IsStill, Is.True);
            Assert.That(Input(Vector2.Zero).Platform, Is.EqualTo(PlatformMotion.None));
        }

        [Test]
        public void RestrainingAndRulesKeepThePlatform()
        {
            var motion = new PlatformMotion(Vector3.Zero, new Vector3(0.1f, 0f, 0f), 0.2f);
            var input = Input(new Vector2(0f, 1f)).WithPlatform(motion);
            var downed = new CharacterMotorInput(
                Vector2.Zero, 0f, false, false, false, true, false, false, false, false, false, false, false, 1f, true, 0, motion);

            Assert.That(input.Platform, Is.EqualTo(motion));
            Assert.That(input.WithRules(ActionRules.Default).Platform, Is.EqualTo(motion));
            Assert.That(input.WithCarrySpeedMultiplier(0.5f).WithLean(1).Platform, Is.EqualTo(motion));
            Assert.That(downed.Restrained().Platform, Is.EqualTo(motion));
            Assert.That(input.WithPlatform(PlatformMotion.None).Platform.IsStill, Is.True);
        }

        [Test]
        public void TheMotionRejectsValuesThatAreNotFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformMotion(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformMotion(Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformMotion(Vector3.Zero, Vector3.Zero, float.NaN));
        }

        [Test]
        public void TheMotionTurnsAPointTheSameWayAsTheYawForward()
        {
            var quarter = MathF.PI / 2f;
            var motion = new PlatformMotion(new Vector3(1f, 0f, 1f), new Vector3(0f, 2f, 0f), quarter);

            var carried = motion.Carry(new Vector3(1f, 5f, 2f));
            var forward = PlatformMotion.RotateY(CharacterMotorSimulation.YawForward(0f), quarter);

            Assert.That(carried.X, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(carried.Y, Is.EqualTo(7f).Within(1e-5f));
            Assert.That(carried.Z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(motion.Turn(0f), Is.EqualTo(quarter));
            Assert.That(forward.X, Is.EqualTo(CharacterMotorSimulation.YawForward(quarter).X).Within(1e-5f));
            Assert.That(forward.Z, Is.EqualTo(CharacterMotorSimulation.YawForward(quarter).Z).Within(1e-5f));
        }

        [Test]
        public void StandingOnASlidingPlatformMovesWithIt()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var start = state.Position;
            var step = new Vector3(2f * _tick, 0f, 0f);

            for (var i = 0; i < 60; i++)
            {
                var motion = MovePlatform(step);
                state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(motion));
                Assert.That(state.IsGrounded, Is.True, $"tick {i} should stay on the platform");
            }

            Assert.That(state.Position.X - start.X, Is.EqualTo(2f).Within(0.01f));
            Assert.That(state.Position.Y, Is.EqualTo(start.Y).Within(0.01f));
            Assert.That(state.Position.Z, Is.EqualTo(start.Z).Within(1e-4f));
            Assert.That(state.Velocity.X, Is.EqualTo(0f).Within(1e-4f), "the speed on the platform is relative to the platform");
        }

        [Test]
        public void WalkingOnASlidingPlatformAddsTheWalkToTheRide()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var start = state.Position;
            var step = new Vector3(2f * _tick, 0f, 0f);

            // 0.5 秒だけ歩き、板の縁 (半幅 4) より手前で止める
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f)).WithPlatform(MovePlatform(step)));
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.X - start.X, Is.EqualTo(1f).Within(0.01f));
            Assert.That(state.Position.Z - start.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed * 0.5f).Within(0.02f));
        }

        [Test]
        public void ARisingElevatorLiftsTheBodyAndKeepsItGrounded()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var start = state.Position;
            var step = new Vector3(0f, 1.5f * _tick, 0f);

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(MovePlatform(step)));
                Assert.That(state.IsGrounded, Is.True, $"tick {i} should stay on the elevator");
            }

            Assert.That(state.Position.Y - start.Y, Is.EqualTo(1.5f).Within(0.02f));
        }

        [Test]
        public void AFallingElevatorLowersTheBodyAndKeepsItGrounded()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var start = state.Position;
            var step = new Vector3(0f, -3f * _tick, 0f);

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(MovePlatform(step)));
                Assert.That(state.IsGrounded, Is.True, $"tick {i} should stay on the elevator");
            }

            Assert.That(state.Position.Y - start.Y, Is.EqualTo(-3f).Within(0.02f));
        }

        [Test]
        public void ASpinningPlatformCarriesTheBodyRoundAndTurnsTheView()
        {
            var simulation = NewSimulation(new Vector3(1.5f, 1.2f, 0f));
            var state = Settle(simulation);
            var yaw = 0f;
            var turn = MathF.PI / 2f * _tick;

            for (var i = 0; i < 60; i++)
            {
                var motion = new PlatformMotion(_platformTop, Vector3.Zero, turn);
                state = simulation.Step(state, Input(Vector2.Zero, yaw).WithPlatform(motion));
                yaw = simulation.LastRide.Motion.Turn(yaw);
            }

            // 上から見て時計回りに 90 度回ると、+X にいた体は -Z へ来る
            Assert.That(state.Position.X, Is.EqualTo(0f).Within(0.02f));
            Assert.That(state.Position.Z, Is.EqualTo(-1.5f).Within(0.02f));
            Assert.That(yaw, Is.EqualTo(MathF.PI / 2f).Within(1e-4f));
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void JumpingOffAMovingPlatformKeepsItsSpeed()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var speed = 3f;
            var step = new Vector3(speed * _tick, 0f, 0f);
            for (var i = 0; i < 10; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(MovePlatform(step)));
            }

            state = simulation.Step(state, Input(Vector2.Zero, jump: true).WithPlatform(MovePlatform(step)));

            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.Velocity.X, Is.EqualTo(speed).Within(1e-3f));
            Assert.That(state.Velocity.Y, Is.GreaterThan(0f));

            var x = state.Position.X;
            state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(MovePlatform(step)));

            // 宙に出た後は床に運ばれず、引き継いだ速さを空中の制御で落としながら進む
            var expected = speed * (1f - MotorSpec.DefaultAirControl) * _tick;
            Assert.That(state.Position.X - x, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(simulation.LastRide.IsRiding, Is.False);
        }

        [Test]
        public void WalkingOffAMovingPlatformKeepsItsSpeed()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var step = new Vector3(0f, 0f, -2f * _tick);
            var leftAt = -1;

            for (var i = 0; i < 300 && leftAt < 0; i++)
            {
                state = simulation.Step(state, Input(new Vector2(0f, 1f), sprint: true).WithPlatform(MovePlatform(step)));
                if (!state.IsGrounded)
                {
                    leftAt = i;
                }
            }

            Assert.That(leftAt, Is.GreaterThanOrEqualTo(0), "the body should run off the far edge");
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed - 2f).Within(1e-3f));
        }

        [Test]
        public void ARidingBodyIsNotPushedIntoTheCeiling()
        {
            var ceilingBottom = 3f;
            AddStaticBox(new Vector3(0f, ceilingBottom + 0.5f, 0f), new Vector3(_halfWidth, 0.5f, _halfWidth));
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var step = new Vector3(0f, 2f * _tick, 0f);
            var blocked = false;

            for (var i = 0; i < 60 && !blocked; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero).WithPlatform(MovePlatform(step)));
                blocked = simulation.LastRide.IsBlocked;
                Assert.That(state.Position.Y + 1f, Is.LessThanOrEqualTo(ceilingBottom + 1e-4f), $"tick {i} pushed the head into the ceiling");
            }

            Assert.That(blocked, Is.True, "the rise should be blocked by the ceiling");
            Assert.That(simulation.LastRide.Velocity(_tick).Y, Is.EqualTo(0f), "no upward speed is kept from a blocked rise");
        }

        [Test]
        public void TheRidesAreTheSameBitForBit()
        {
            var first = RunCourse();
            SetUp();
            var second = RunCourse();

            Assert.That(second, Is.EqualTo(first));
        }

        private List<CharacterMotorState> RunCourse()
        {
            var simulation = NewSimulation();
            var state = Settle(simulation);
            var states = new List<CharacterMotorState>();
            for (var i = 0; i < 120; i++)
            {
                var motion = MovePlatform(new Vector3(0.02f, 0.01f, -0.01f), 0.01f);
                state = simulation.Step(state, Input(new Vector2(MathF.Sin(i * 0.1f), 0.5f), jump: i == 90).WithPlatform(motion));
                states.Add(state);
            }

            return states;
        }

        private PlatformMotion MovePlatform(Vector3 translation, float yawDelta = 0f)
        {
            var motion = new PlatformMotion(_platformTop, translation, yawDelta);
            _platformTop += translation;
            Rebuild();
            return motion;
        }

        private CharacterMotorSimulation NewSimulation()
        {
            return NewSimulation(new Vector3(0f, 1.2f, 0f));
        }

        private CharacterMotorSimulation NewSimulation(Vector3 spawn)
        {
            Rebuild();
            var body = new CharacterBody(_world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY)
            {
                SpawnPosition = spawn,
            };
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(simulation.SpawnPosition, Vector3.Zero, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the platform before the test");
            return state;
        }

        // 回る床でも体がはみ出ないよう、板は回さずに広い正方形のままにする (当たりの形は運ぶ計算に関わらない)
        private void Rebuild()
        {
            var colliders = new List<WorldCollider>(_statics)
            {
                WorldCollider.Box(
                    100,
                    0,
                    WorldCollider.StaticGroup,
                    new Vector3(_platformTop.X, _platformTop.Y - _halfThickness, _platformTop.Z),
                    new Vector3(_halfWidth, _halfThickness, _halfWidth),
                    Quaternion.Identity),
            };
            _world.Inner = new BvhWorldCollision(new WorldMap(colliders));
        }

        private void AddStaticBox(Vector3 center, Vector3 halfExtents)
        {
            _statics.Add(WorldCollider.Box(_statics.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }

        private static CharacterMotorInput Input(Vector2 move, float yaw = 0f, bool jump = false, bool sprint = false)
        {
            return new CharacterMotorInput(move, yaw, sprint, jump, false, false, false, false, false, false);
        }

        // 床が動くたびに当たりを組み直すため、体が握る世界の中身だけを差し替える
        private sealed class SwappableWorld : IWorldCollision
        {
            public IWorldCollision Inner { get; set; }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                return Inner.Raycast(origin, direction, maxDistance, layerMask, out hit);
            }

            public bool RaycastCollider(int colliderId, Vector3 origin, Vector3 direction, float maxDistance, out WorldHit hit)
            {
                return Inner.RaycastCollider(colliderId, origin, direction, maxDistance, out hit);
            }

            public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
            {
                return Inner.OverlapCapsule(point0, point1, radius, layerMask, results);
            }

            public bool CapsuleCast(
                Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                return Inner.CapsuleCast(point0, point1, radius, direction, maxDistance, layerMask, out hit);
            }
        }
    }
}
