using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SwimStepTests
    {
        // 1/64 にするのは、息の秒数を足し続けても丸めずに数えられ、溺れの出来事が出る tick を式どおりに書けるため
        private const float _tick = 1f / 64f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 192;

        private static readonly int[] _expectedDrownedTicks = { 4, 8, 12 };

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheChestIsBetweenTheCapsuleCenterAndTheEyes()
        {
            Assert.That(SwimStep.ChestY(_stance), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(SwimStep.FloatY(2f, _stance), Is.EqualTo(1.7f).Within(1e-6f));
            Assert.Throws<ArgumentNullException>(() => SwimStep.ChestY(null));
        }

        [Test]
        public void WaterAboveTheChestStartsASwimAndWaterAtTheChestDoesNot()
        {
            var tuning = SwimTuning.Default;

            var deep = SwimStep.Step(CharacterSwimState.None, Vector3.Zero, true, _stance, new Pool(0.31f), tuning, _tick);
            var level = SwimStep.Step(CharacterSwimState.None, Vector3.Zero, true, _stance, new Pool(0.3f), tuning, _tick);
            var dry = SwimStep.Step(CharacterSwimState.None, Vector3.Zero, true, _stance, NoWaterVolumes.Instance, tuning, _tick);

            Assert.That(deep.IsSwimming, Is.True);
            Assert.That(deep.SurfaceY, Is.EqualTo(0.31f));
            Assert.That(deep.IsUnderwater, Is.False, "the eyes at 0.6 are above the surface");
            Assert.That(level, Is.EqualTo(CharacterSwimState.None));
            Assert.That(dry, Is.EqualTo(CharacterSwimState.None));
        }

        [Test]
        public void ASwimEndsOnlyWhenTheFeetTouchShallowWaterOrLeaveIt()
        {
            var swimming = new CharacterSwimState(true, 0.31f, false);
            var tuning = SwimTuning.Default;

            var floating = SwimStep.Step(swimming, Vector3.Zero, false, _stance, new Pool(0.2f), tuning, _tick);
            var standing = SwimStep.Step(swimming, Vector3.Zero, true, _stance, new Pool(0.2f), tuning, _tick);
            var deepFloor = SwimStep.Step(swimming, Vector3.Zero, true, _stance, new Pool(0.5f), tuning, _tick);
            var above = SwimStep.Step(swimming, new Vector3(0f, 2f, 0f), false, _stance, new Pool(0.5f), tuning, _tick);

            Assert.That(floating.IsSwimming, Is.True, "water below the chest keeps a swim that has not reached the floor");
            Assert.That(standing, Is.EqualTo(CharacterSwimState.None));
            Assert.That(deepFloor.IsSwimming, Is.True, "the floor under deep water does not end the swim");
            Assert.That(above, Is.EqualTo(CharacterSwimState.None), "the feet at 1 are above the surface at 0.5");
        }

        [Test]
        public void TheBreathRunsOutAfterTheBreathSecondsAndThenEveryBreathSeconds()
        {
            var tuning = new SwimTuning(3f, 12f, 1f);
            var water = new Pool(1f);
            var swim = CharacterSwimState.None;
            var drownedAt = new List<int>();

            for (var tick = 1; tick <= 13; tick++)
            {
                var next = SwimStep.Step(swim, Vector3.Zero, false, _stance, water, tuning, 0.25f);
                if (next.DrowningsSince(swim) > 0)
                {
                    drownedAt.Add(tick);
                }

                swim = next;
            }

            Assert.That(swim.IsUnderwater, Is.True);
            Assert.That(drownedAt, Is.EqualTo(_expectedDrownedTicks), "1 s is held, so the first event comes when the time reaches 1 s, then every drown interval");
            Assert.That(swim.Drownings, Is.EqualTo(3));
            Assert.That(swim.BreathUsedSeconds, Is.EqualTo(3.25f));
        }

        [Test]
        public void SurfacingRestoresTheBreathButKeepsTheEvents()
        {
            var tuning = new SwimTuning(3f, 12f, 1f);
            var swim = new CharacterSwimState(true, 1f, true, 0.75f, 2);

            var surfaced = SwimStep.Step(swim, Vector3.Zero, false, _stance, new Pool(0.5f), tuning, 0.25f);

            Assert.That(surfaced.IsSwimming, Is.True);
            Assert.That(surfaced.IsUnderwater, Is.False);
            Assert.That(surfaced.BreathUsedSeconds, Is.Zero);
            Assert.That(surfaced.Drownings, Is.EqualTo(2));
        }

        [Test]
        public void ATickLongerThanTheBreathCountsEveryEventAtOnce()
        {
            var tuning = new SwimTuning(3f, 12f, 1f);

            var swim = SwimStep.Step(CharacterSwimState.None, Vector3.Zero, false, _stance, new Pool(1f), tuning, 2.5f);

            Assert.That(swim.Drownings, Is.EqualTo(2));
            Assert.That(swim.BreathUsedSeconds, Is.EqualTo(2.5f));
        }

        [Test]
        public void DrowningsSinceCountsOnlyNewEventsOfTheSameSwim()
        {
            var before = new CharacterSwimState(true, 1f, true, 0f, 1);
            var after = new CharacterSwimState(true, 1f, true, 0f, 3);

            Assert.That(after.DrowningsSince(before), Is.EqualTo(2));
            Assert.That(after.DrowningsSince(CharacterSwimState.None), Is.EqualTo(3), "a new swim counts from zero");
            Assert.That(CharacterSwimState.None.DrowningsSince(after), Is.Zero);
            Assert.That(before.DrowningsSince(after), Is.Zero);
        }

        [Test]
        public void TheSwimStateForgetsTheBreathItDoesNotUse()
        {
            Assert.That(new CharacterSwimState(true, 1f, false, 0.7f, 2).BreathUsedSeconds, Is.Zero, "the breath only counts underwater");
            Assert.That(new CharacterSwimState(true, 1f, false, 0.7f, 2).Drownings, Is.EqualTo(2));
            Assert.That(new CharacterSwimState(false, 1f, true, 0.7f, 2), Is.EqualTo(CharacterSwimState.None));
            Assert.That(new CharacterSwimState(true, 1f, true, 0.7f, 2), Is.Not.EqualTo(new CharacterSwimState(true, 1f, true, 0.7f, 3)));
            Assert.That(new CharacterSwimState(true, 1f, true, -0.1f, 0).IsValid, Is.False);
            Assert.That(new CharacterSwimState(true, 1f, true, float.NaN, 0).IsValid, Is.False);
            Assert.That(new CharacterSwimState(true, 1f, true, 0f, -1).IsValid, Is.False);
        }

        [Test]
        public void IsInWaterIsWhetherTheFeetAreBelowTheSurface()
        {
            Assert.That(SwimStep.IsInWater(new Pool(0.5f), new Vector3(0f, 0f, 0f)), Is.True);
            Assert.That(SwimStep.IsInWater(new Pool(0.5f), new Vector3(0f, 0.5f, 0f)), Is.False);
            Assert.That(SwimStep.IsInWater(NoWaterVolumes.Instance, Vector3.Zero), Is.False);
            Assert.Throws<ArgumentNullException>(() => SwimStep.IsInWater(null, Vector3.Zero));
        }

        [Test]
        public void ALandingInWaterDealsNoFallDamage()
        {
            var spec = MotorSpec.Default;

            Assert.That(FallDamageCalculator.Calculate(30f, spec, 100, false), Is.EqualTo(100));
            Assert.That(FallDamageCalculator.Calculate(30f, spec, 100, true), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => FallDamageCalculator.Calculate(-1f, spec, 100, true));
        }

        [Test]
        public void EnteringTheWaterSwimsAndTheBuoyancyFloatsTheChestAtTheSurface()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var state = new CharacterMotorState(new Vector3(0f, -0.8f, 0f), Vector3.Zero, false, false);

            var first = simulation.Step(state, Input());

            Assert.That(first.IsSwimming, Is.True);
            Assert.That(first.Swim.SurfaceY, Is.Zero);
            Assert.That(first.Swim.IsUnderwater, Is.True, "the eyes started at -0.2");
            Assert.That(first.Velocity.Y, Is.EqualTo(12f * _tick).Within(1e-6f), "the buoyancy of 12 m/s² rises instead of gravity");
            Assert.That(first.CanAimDownSights, Is.False);

            var floated = Run(simulation, first, Input(), _settleTicks);

            Assert.That(floated.IsSwimming, Is.True);
            Assert.That(floated.Position.Y, Is.EqualTo(-0.3f).Within(1e-3f));
            Assert.That(floated.Velocity.Y, Is.Zero);
            Assert.That(floated.Swim.IsUnderwater, Is.False);
            Assert.That(floated.Swim.BreathUsedSeconds, Is.Zero);
            Assert.That(Run(simulation, floated, Input(), 64).Position.Y, Is.EqualTo(floated.Position.Y).Within(1e-5f), "it does not bob at the surface");
        }

        [Test]
        public void CrouchDivesAndJumpRisesAtTheSwimSpeed()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var floated = Float(simulation, new Vector3(0f, -0.8f, 0f));

            var dived = Run(simulation, floated, Input(crouch: true), 32);

            Assert.That(dived.Velocity.Y, Is.EqualTo(-SwimTuning.DefaultSpeed));
            Assert.That(dived.Position.Y, Is.EqualTo(floated.Position.Y - 1.5f).Within(1e-3f));
            Assert.That(dived.Stance, Is.EqualTo(PlayerStance.Standing), "crouch dives instead of crouching");
            Assert.That(dived.Swim.IsUnderwater, Is.True);

            var rising = simulation.Step(dived, Input(jump: true));

            Assert.That(rising.Velocity.Y, Is.EqualTo(SwimTuning.DefaultSpeed));
            Assert.That(rising.Position.Y, Is.EqualTo(dived.Position.Y + (SwimTuning.DefaultSpeed * _tick)).Within(1e-4f));

            var back = Run(simulation, rising, Input(), _settleTicks);

            Assert.That(back.Position.Y, Is.EqualTo(-0.3f).Within(1e-3f));
            Assert.That(back.Swim.IsUnderwater, Is.False);
        }

        [Test]
        public void SprintProneDodgeAndCoverDoNothingWhileSwimming()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var floated = Float(simulation, new Vector3(0f, -0.8f, 0f));
            var everything = new CharacterMotorInput(
                new Vector2(0f, 1f), 0f, true, false, false, false, true, true, true, true, true, true, true);

            var next = simulation.Step(floated, everything);

            Assert.That(next.IsSwimming, Is.True);
            Assert.That(next.Velocity.Z, Is.EqualTo(SwimTuning.DefaultSpeed).Within(1e-4f), "sprint does not speed up a swim");
            Assert.That(next.Stance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(next.Prone.IsActive, Is.False);
            Assert.That(next.IsDodging, Is.False);
            Assert.That(next.Cover.IsInCover, Is.False);
            Assert.That(next.CanAimDownSights, Is.False);
        }

        [Test]
        public void TheCarryLoadSlowsTheSwimToo()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var floated = Float(simulation, new Vector3(0f, -0.8f, 0f));

            var next = simulation.Step(floated, Move(new Vector2(0f, 1f)).WithCarrySpeedMultiplier(0.5f));

            Assert.That(next.IsSwimming, Is.True);
            Assert.That(next.Velocity.Z, Is.EqualTo(SwimTuning.DefaultSpeed * 0.5f).Within(1e-4f));
        }

        [Test]
        public void ADownedSwimmerFloatsInsteadOfDiving()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var floated = Float(simulation, new Vector3(0f, -0.8f, 0f));
            var downed = new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, true, false, false, false, false);

            var next = Run(simulation, floated, downed, 64);

            Assert.That(next.IsSwimming, Is.True);
            Assert.That(next.Position.Y, Is.EqualTo(-0.3f).Within(1e-3f));
        }

        [Test]
        public void TheSwimEndsAndWalkingReturnsWhenTheWaterGetsShallow()
        {
            AddBox(new Vector3(0f, -2.5f, 0f), new Vector3(50f, 1f, 50f));
            var water = new Pool(0f);
            var simulation = NewSimulation(water);
            var floated = Float(simulation, new Vector3(0f, -0.45f, 0f));
            Assert.That(floated.IsGrounded, Is.False, "the feet float 0.2 above the floor");

            water.SurfaceY = -0.5f;
            var state = floated;
            for (var i = 0; i < 128 && state.IsSwimming; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsSwimming, Is.False);
            state = Run(simulation, state, Input(), _settleTicks);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(-0.5f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.01f), "standing on the floor at -1.5, a skin width above it");

            var jumped = simulation.Step(state, Input(jump: true));
            var spec = MotorSpec.Default;

            // PlayerMotor は跳んだ tick にも重力を 1 tick 分かける
            Assert.That(jumped.IsSwimming, Is.False);
            Assert.That(
                jumped.Velocity.Y,
                Is.EqualTo(MathF.Sqrt(2f * spec.Gravity * spec.JumpHeight) - (spec.Gravity * _tick)).Within(1e-3f),
                "a jump on the floor is a walking jump again");
        }

        [Test]
        public void JumpingAtALedgeWithinReachClimbsOutWithTheVault()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));

            AddBox(new Vector3(0f, -1.65f, 10.8f), new Vector3(50f, 1.35f, 10f));
            var simulation = NewSimulation(new Pool(0f, maxZ: 0.8f));
            var floated = Float(simulation, new Vector3(0f, -0.5f, 0f));

            var started = simulation.Step(floated, Input(jump: true));

            Assert.That(started.IsVaulting, Is.True);

            var state = started;
            for (var i = 0; i < 256 && state.IsVaulting; i++)
            {
                state = simulation.Step(state, Input());
            }

            state = Run(simulation, state, Input(), 8);

            Assert.That(state.IsVaulting, Is.False);
            Assert.That(state.IsSwimming, Is.False);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Z, Is.GreaterThan(1.3f), "the body stands past the edge");
            Assert.That(state.Position.Y, Is.EqualTo(0.7f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.01f), "the feet stand on the ledge top at -0.3, a skin width above it");
        }

        [Test]
        public void StayingUnderLongerThanTheBreathStacksDrowningEvents()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            simulation.Swim = new SwimTuning(3f, 12f, 0.5f, 10, 0.5f);
            var state = Float(simulation, new Vector3(0f, -0.8f, 0f));
            var underwaterTicks = 0;
            var seen = 0;

            for (var i = 0; i < 160; i++)
            {
                var next = simulation.Step(state, Input(crouch: true));
                seen += next.Swim.DrowningsSince(state.Swim);
                if (next.Swim.IsUnderwater)
                {
                    underwaterTicks++;
                }

                state = next;
            }

            // 息 0.5 s は 32 tick、以後 0.5 s (32 tick) ごとに 1 回
            var expected = underwaterTicks / 32;
            Assert.That(state.IsSwimming, Is.True, "the floor under deep water keeps the swim");
            Assert.That(underwaterTicks, Is.GreaterThan(129));
            Assert.That(state.Swim.Drownings, Is.EqualTo(expected));
            Assert.That(seen, Is.EqualTo(expected));
        }

        [Test]
        public void FallingIntoDeepWaterLosesTheFallSpeedOnEntry()
        {
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            var simulation = NewSimulation(new Pool(0f));
            var state = new CharacterMotorState(new Vector3(0f, 20f, 0f), Vector3.Zero, false, false);
            var before = state;
            for (var i = 0; i < 256 && !state.IsSwimming; i++)
            {
                before = state;
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsSwimming, Is.True);
            Assert.That(
                FallDamageCalculator.Calculate(-before.Velocity.Y, MotorSpec.Default, 100), Is.EqualTo(100), "the same speed on the ground would be lethal");
            Assert.That(state.Velocity.Y, Is.GreaterThanOrEqualTo(-SwimTuning.DefaultSpeed));

            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.Velocity.Y, Is.GreaterThanOrEqualTo(-SwimTuning.DefaultSpeed), "tick " + i);
                Assert.That(state.IsGrounded, Is.False, "the plunge does not reach the floor at -3");
            }

            Assert.That(state.Position.Y, Is.EqualTo(-0.3f).Within(1e-3f));
        }

        [Test]
        public void LandingInShallowWaterDealsNoFallDamage()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
            var water = new Pool(0.5f);
            var simulation = NewSimulation(water);
            var state = new CharacterMotorState(new Vector3(0f, 20f, 0f), Vector3.Zero, false, false);
            for (var i = 0; i < 256 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.IsSwimming, Is.False, "water at 0.5 is below the chest");
            }

            var fallSpeed = -state.Velocity.Y;
            var feet = new Vector3(state.Position.X, state.Position.Y + _stance.Standing.BottomY, state.Position.Z);

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(FallDamageCalculator.Calculate(fallSpeed, MotorSpec.Default, 100), Is.EqualTo(100), "about 27 m/s is above the lethal 25 m/s");
            Assert.That(FallDamageCalculator.Calculate(fallSpeed, MotorSpec.Default, 100, SwimStep.IsInWater(water, feet)), Is.Zero);
        }

        private static CharacterMotorInput Input(bool jump = false, bool crouch = false)
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, jump, crouch, false, false, false, false, true);
        }

        private static CharacterMotorInput Move(Vector2 move)
        {
            return new CharacterMotorInput(move, 0f, false, false, false, false, false, false, false, true);
        }

        private static CharacterMotorState Run(CharacterMotorSimulation simulation, CharacterMotorState state, CharacterMotorInput input, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                state = simulation.Step(state, input);
            }

            return state;
        }

        private static CharacterMotorState Float(CharacterMotorSimulation simulation, Vector3 start)
        {
            var state = Run(simulation, new CharacterMotorState(start, Vector3.Zero, false, false), Input(), _settleTicks);
            Assert.That(state.IsSwimming, Is.True, "the body should float before the test");
            Assert.That(state.Position.Y, Is.EqualTo(-0.3f).Within(1e-3f), "the chest should be at the surface before the test");
            return state;
        }

        private CharacterMotorSimulation NewSimulation(IWaterVolumes water)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, _killPlaneY)
            {
                Water = water,
            };
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }

        private sealed class Pool : IWaterVolumes
        {
            private readonly float _maxZ;

            public Pool(float surfaceY, float maxZ = float.PositiveInfinity)
            {
                SurfaceY = surfaceY;
                _maxZ = maxZ;
            }

            public float SurfaceY { get; set; }

            public float? SurfaceAt(Vector3 position)
            {
                return position.Z < _maxZ ? SurfaceY : (float?)null;
            }
        }
    }
}
