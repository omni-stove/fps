using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MotorCoverLadderTuningTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const int _settleTicks = 30;
        private const float _radius = 0.5f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, _radius);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);

        // 原点に下端があり、+Z を向いてつかまる高さ 3m・幅 0.8m のはしご。
        private static readonly LadderSpan _ladder = new LadderSpan(Vector3.Zero, Vector3.UnitZ, 3f, 0.8f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void DefaultsAreTheFormerConstants()
        {
            var spec = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f);

            Assert.That(spec.CoverMaxEnterDistance, Is.EqualTo(4f));
            Assert.That(spec.CoverMaxEnterAngleDegrees, Is.EqualTo(45f));
            Assert.That(spec.CoverLowWallHeight, Is.EqualTo(1.1f));
            Assert.That(spec.CoverPeekSideDistance, Is.EqualTo(0.6f));
            Assert.That(spec.CoverEnterSeconds, Is.EqualTo(0.15f));
            Assert.That(spec.CoverEnterSpeed, Is.EqualTo(8f));
            Assert.That(spec.CoverBackHoldSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.CoverPeekSlideSeconds, Is.EqualTo(0.15f));
            Assert.That(spec.LadderTopEntryDepth, Is.EqualTo(0.3f));
            Assert.That(spec.LadderMinApproachDot, Is.EqualTo(0.5f));
            Assert.That(spec.LadderMinApproachSpeed, Is.EqualTo(0.5f));
        }

        [TestCase("coverMaxEnterDistance", -0.1f)]
        [TestCase("coverMaxEnterDistance", float.PositiveInfinity)]
        [TestCase("coverMaxEnterAngleDegrees", -1f)]
        [TestCase("coverMaxEnterAngleDegrees", 181f)]
        [TestCase("coverLowWallHeight", float.NaN)]
        [TestCase("coverPeekSideDistance", -0.1f)]
        [TestCase("coverEnterSeconds", 0f)]
        [TestCase("coverEnterSpeed", 0f)]
        [TestCase("coverEnterSpeed", float.PositiveInfinity)]
        [TestCase("coverBackHoldSeconds", -0.1f)]
        [TestCase("coverPeekSlideSeconds", 0f)]
        [TestCase("ladderTopEntryDepth", -0.1f)]
        [TestCase("ladderMinApproachDot", -1.1f)]
        [TestCase("ladderMinApproachDot", 1.1f)]
        [TestCase("ladderMinApproachSpeed", -0.1f)]
        public void OutOfRangeValuesNameTheParameter(string name, float value)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => SpecWith(name, value));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }

        [Test]
        public void TheRulesFileReadsBackTheValues()
        {
            var text = NetMatchFile.Write(FileWith(Settings(Tuned())));

            var read = NetMatchFile.Parse(text, "motor.json").Motor.Motor;

            Assert.That(text, Does.Contain("\"coverMaxEnterDistance\": 2"));
            Assert.That(text, Does.Contain("\"ladderMinApproachSpeed\": 1.5"));
            Assert.That(text, Does.Contain("\"coverEnterSpeed\": 3"));
            AssertTuned(read);
        }

        [Test]
        public void TheRulesFileDefaultsTheValuesLeftOut()
        {
            var text = "{\"motor\": {\"walkSpeed\": 6}, \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + TestRules.HandgunId
                + "\"]}, \"weapons\": [{\"id\": \"" + TestRules.HandgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\","
                + " \"magazineSize\": 12, \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var read = NetMatchFile.Parse(text, "partial.json").Motor.Motor;

            Assert.That(read.WalkSpeed, Is.EqualTo(6f));
            Assert.That(read.CoverMaxEnterDistance, Is.EqualTo(MotorSpec.DefaultCoverMaxEnterDistance));
            Assert.That(read.CoverMaxEnterAngleDegrees, Is.EqualTo(MotorSpec.DefaultCoverMaxEnterAngleDegrees));
            Assert.That(read.CoverLowWallHeight, Is.EqualTo(MotorSpec.DefaultCoverLowWallHeight));
            Assert.That(read.CoverPeekSideDistance, Is.EqualTo(MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(read.CoverEnterSeconds, Is.EqualTo(MotorSpec.DefaultCoverEnterSeconds));
            Assert.That(read.CoverEnterSpeed, Is.EqualTo(MotorSpec.DefaultCoverEnterSpeed));
            Assert.That(read.CoverBackHoldSeconds, Is.EqualTo(MotorSpec.DefaultCoverBackHoldSeconds));
            Assert.That(read.CoverPeekSlideSeconds, Is.EqualTo(MotorSpec.DefaultCoverPeekSlideSeconds));
            Assert.That(read.LadderTopEntryDepth, Is.EqualTo(MotorSpec.DefaultLadderTopEntryDepth));
            Assert.That(read.LadderMinApproachDot, Is.EqualTo(MotorSpec.DefaultLadderMinApproachDot));
            Assert.That(read.LadderMinApproachSpeed, Is.EqualTo(MotorSpec.DefaultLadderMinApproachSpeed));
        }

        [TestCase("{\"motor\": {\"coverMaxEnterDistance\": -1}}", "motor.coverMaxEnterDistance")]
        [TestCase("{\"motor\": {\"coverMaxEnterAngleDegrees\": 200}}", "motor.coverMaxEnterAngleDegrees")]
        [TestCase("{\"motor\": {\"coverEnterSeconds\": 0}}", "motor.coverEnterSeconds")]
        [TestCase("{\"motor\": {\"coverEnterSpeed\": 0}}", "motor.coverEnterSpeed")]
        [TestCase("{\"motor\": {\"coverPeekSlideSeconds\": 0}}", "motor.coverPeekSlideSeconds")]
        [TestCase("{\"motor\": {\"ladderMinApproachDot\": 2}}", "motor.ladderMinApproachDot")]
        [TestCase("{\"motor\": {\"ladderTopEntryDepth\": -1}}", "motor.ladderTopEntryDepth")]
        public void BadValuesInTheRulesFileNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void ConnectAcceptedCarriesTheValues()
        {
            var writer = new MessageWriter();
            var message = new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 };
            message.Motor = Settings(Tuned());
            writer.WriteMessage(message);

            var got = (ConnectAcceptedMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment());

            AssertTuned(got.Motor.Motor);
            Assert.That(got.Motor.Motor.VaultMaxDepth, Is.EqualTo(MotorSpec.DefaultVaultMaxDepth), "the values before the new ones still line up");
            Assert.That(got.Motor.Motor.WalkDirection.Forward, Is.EqualTo(1f), "the values after the new ones still line up");
            Assert.That(got.Motor.Prone.DiveSeconds, Is.EqualTo(ProneSpec.DefaultDiveSeconds));
        }

        [TestCase(4.5f)]
        [TestCase(5.9f)]
        public void AWallBeyondTheDefaultEnterDistanceCanBeEnteredWithALongerOne(float distance)
        {
            var sample = new CoverWallSample(distance, _wallNormal, 2f, true, true);

            Assert.That(CoverProbe.Evaluate(sample, Vector3.UnitZ, MotorSpec.Default).CanEnter, Is.False, "the default reaches 4 m");
            Assert.That(CoverProbe.Evaluate(sample, Vector3.UnitZ, SpecWith("coverMaxEnterDistance", 6f)).CanEnter, Is.True);
        }

        [Test]
        public void TheProbeUsesTheConfiguredAngleAndLowWallHeight()
        {
            var sample = new CoverWallSample(0.8f, _wallNormal, 1.3f, true, true);
            var turned = new Vector3(MathF.Sin(0.7f), 0f, MathF.Cos(0.7f));

            Assert.That(CoverProbe.Evaluate(sample, turned, MotorSpec.Default).CanEnter, Is.True, "40 degrees is inside the default 45");
            Assert.That(CoverProbe.Evaluate(sample, turned, SpecWith("coverMaxEnterAngleDegrees", 30f)).CanEnter, Is.False);
            Assert.That(CoverProbe.Evaluate(sample, Vector3.UnitZ, MotorSpec.Default).Height, Is.EqualTo(CoverHeight.High));
            Assert.That(CoverProbe.Evaluate(sample, Vector3.UnitZ, SpecWith("coverLowWallHeight", 1.5f)).Height, Is.EqualTo(CoverHeight.Low));
        }

        [Test]
        public void ThePeekStepsOutTheConfiguredDistance()
        {
            var edge = CoverProbe.Evaluate(new CoverWallSample(0.8f, _wallNormal, 2f, false, true), Vector3.UnitZ, MotorSpec.Default);

            Assert.That(CoverProbe.PeekOffset(edge, 1f, MotorSpec.Default), Is.EqualTo(-0.6f));
            Assert.That(CoverProbe.PeekOffset(edge, 1f, SpecWith("coverPeekSideDistance", 1.1f)), Is.EqualTo(-1.1f));
        }

        [Test]
        public void TheCoverStateUsesTheConfiguredSeconds()
        {
            var wall = new CoverProbeResult(true, true, CoverHeight.High, CoverEdge.None, _wallNormal, 0.55f);
            var state = new CoverState(SpecWith("coverEnterSeconds", 0.5f));
            state.Step(true, false, Vector2.Zero, wall, 0f);

            state.Step(false, false, Vector2.Zero, wall, 0.25f);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Entering), "0.25 s is half of 0.5 s");
            Assert.That(state.EnterStep, Is.EqualTo(0.25f).Within(1e-5f), "half of the 0.5 m gap in half of the time");
            state.Step(false, false, Vector2.Zero, wall, 0.25f);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));

            state.Spec = SpecWith("coverBackHoldSeconds", 1f);
            state.Step(false, false, new Vector2(0f, -1f), wall, 0.6f);
            Assert.That(state.IsInCover, Is.True, "0.6 s is shorter than the 1 s back hold");
            Assert.That(state.Step(false, false, new Vector2(0f, -1f), wall, 0.4f), Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.LastExitReason, Is.EqualTo(CoverState.ReasonBack));
        }

        [Test]
        public void TheCoverStateRejectsANullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new CoverState(null));
            Assert.Throws<ArgumentNullException>(() => new CoverState(MotorSpec.Default).Spec = null);
            Assert.Throws<ArgumentNullException>(() => CoverProbe.Evaluate(CoverWallSample.None, Vector3.UnitZ, null));
        }

        [Test]
        public void TheSimulationEntersAWall4Point5MetersAwayOnlyWithA6MeterEnterDistance()
        {
            AddFloor();
            AddBox(new Vector3(0f, 1f, 4.5f + 0.2f), new Vector3(2f, 1f, 0.2f));

            Assert.That(PressCover(MotorSpec.Default).Cover.IsInCover, Is.False, "the default reaches 4 m");
            Assert.That(PressCover(SpecWith("coverMaxEnterDistance", 6f)).Cover.IsInCover, Is.True);
        }

        // 3 m 先の壁まで、体の半径 0.5 m と隙間 0.05 m を残した 2.45 m を走る。1 tick 目は入るだけで動かない。
        [TestCase(MotorSpec.DefaultCoverEnterSpeed, 20)]
        [TestCase(4f, 38)]
        public void TheSimulationRunsToAFarWallAtTheConfiguredSpeed(float speed, int expectedTicks)
        {
            AddFloor();
            AddBox(new Vector3(0f, 1f, 3f + 0.2f), new Vector3(2f, 1f, 0.2f));
            var simulation = NewSimulation(SpecWith("coverEnterSpeed", speed));
            var state = simulation.Step(Settle(simulation), Input(cover: true));
            Assert.That(state.Cover.Snapshot.Phase, Is.EqualTo(CoverPhase.Entering));
            var ticks = 1;
            while (state.Cover.Snapshot.Phase == CoverPhase.Entering && ticks < 600)
            {
                state = simulation.Step(state, Input());
                ticks++;
            }

            Assert.That(state.Cover.Snapshot.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(ticks, Is.EqualTo(expectedTicks).Within(1));
            Assert.That(state.Position.Z, Is.EqualTo(3f - 0.55f).Within(0.03f), "stops at the hold distance from the wall");
        }

        [Test]
        public void TheSimulationPeeksAtTheConfiguredSpeed()
        {
            Assert.That(PeekTicks(SpecWith("coverPeekSlideSeconds", 0.5f)), Is.EqualTo(30).Within(1), "0.6 m in 0.5 s");
            Assert.That(PeekTicks(MotorSpec.Default), Is.EqualTo(9).Within(1), "0.6 m in the default 0.15 s");
        }

        [Test]
        public void TheLadderProbeUsesTheConfiguredApproach()
        {
            var feet = new Vector3(0f, 0f, -0.6f);
            var slow = new Vector3(0f, 0f, 0.8f);
            var slanted = new Vector3(MathF.Sin(1f), 0f, MathF.Cos(1f)) * 3f;

            Assert.That(_ladder.Probe(feet, slow, _radius, LadderSpec.DefaultGrabDistance, MotorSpec.Default), Is.EqualTo(LadderEntry.Front));
            Assert.That(
                _ladder.Probe(feet, slow, _radius, LadderSpec.DefaultGrabDistance, SpecWith("ladderMinApproachSpeed", 1f)),
                Is.EqualTo(LadderEntry.None),
                "0.8 m/s is slower than 1 m/s");
            Assert.That(_ladder.Probe(feet, slanted, _radius, LadderSpec.DefaultGrabDistance, MotorSpec.Default), Is.EqualTo(LadderEntry.Front), "cos 57° ≈ 0.54");
            Assert.That(
                _ladder.Probe(feet, slanted, _radius, LadderSpec.DefaultGrabDistance, SpecWith("ladderMinApproachDot", 0.8f)),
                Is.EqualTo(LadderEntry.None));
        }

        [Test]
        public void TheLadderTopEntryDepthMovesTheGrabHeight()
        {
            var deep = SpecWith("ladderTopEntryDepth", 1f);
            var nearTop = new Vector3(0f, 2.5f, -0.6f);
            var toward = new Vector3(0f, 0f, 3f);

            Assert.That(_ladder.Probe(nearTop, toward, _radius, LadderSpec.DefaultGrabDistance, MotorSpec.Default), Is.EqualTo(LadderEntry.Front));
            Assert.That(_ladder.Probe(nearTop, toward, _radius, LadderSpec.DefaultGrabDistance, deep), Is.EqualTo(LadderEntry.None), "2.5 m is above 3 - 1 m");
            Assert.That(new LadderClimb(_ladder, LadderSpec.Default, LadderEntry.Top, 3f, deep).Height, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(new LadderClimb(_ladder, LadderSpec.Default, LadderEntry.Top, 3f, MotorSpec.Default).Height, Is.EqualTo(2.7f).Within(1e-5f));
            Assert.That(
                LadderClimb.Resume(_ladder, LadderSpec.Default, LadderEntry.Front, 2.9f, false, 0, deep).Height,
                Is.EqualTo(2.9f).Within(1e-5f),
                "a resumed climb keeps the height it had");
        }

        private static MotorSpec Tuned()
        {
            return new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                coverMaxEnterDistance: 2f,
                coverMaxEnterAngleDegrees: 30f,
                coverLowWallHeight: 1.4f,
                coverPeekSideDistance: 0.9f,
                coverEnterSeconds: 0.4f,
                coverEnterSpeed: 3f,
                coverBackHoldSeconds: 0.7f,
                coverPeekSlideSeconds: 0.25f,
                ladderTopEntryDepth: 0.6f,
                ladderMinApproachDot: 0.75f,
                ladderMinApproachSpeed: 1.5f);
        }

        private static void AssertTuned(MotorSpec read)
        {
            Assert.That(read.CoverMaxEnterDistance, Is.EqualTo(2f));
            Assert.That(read.CoverMaxEnterAngleDegrees, Is.EqualTo(30f));
            Assert.That(read.CoverLowWallHeight, Is.EqualTo(1.4f));
            Assert.That(read.CoverPeekSideDistance, Is.EqualTo(0.9f));
            Assert.That(read.CoverEnterSeconds, Is.EqualTo(0.4f));
            Assert.That(read.CoverEnterSpeed, Is.EqualTo(3f));
            Assert.That(read.CoverBackHoldSeconds, Is.EqualTo(0.7f));
            Assert.That(read.CoverPeekSlideSeconds, Is.EqualTo(0.25f));
            Assert.That(read.LadderTopEntryDepth, Is.EqualTo(0.6f));
            Assert.That(read.LadderMinApproachDot, Is.EqualTo(0.75f));
            Assert.That(read.LadderMinApproachSpeed, Is.EqualTo(1.5f));
        }

        private static MotorSpec SpecWith(string name, float value)
        {
            float Pick(string item, float fallback) => item == name ? value : fallback;
            return new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                coverMaxEnterDistance: Pick("coverMaxEnterDistance", MotorSpec.DefaultCoverMaxEnterDistance),
                coverMaxEnterAngleDegrees: Pick("coverMaxEnterAngleDegrees", MotorSpec.DefaultCoverMaxEnterAngleDegrees),
                coverLowWallHeight: Pick("coverLowWallHeight", MotorSpec.DefaultCoverLowWallHeight),
                coverPeekSideDistance: Pick("coverPeekSideDistance", MotorSpec.DefaultCoverPeekSideDistance),
                coverEnterSeconds: Pick("coverEnterSeconds", MotorSpec.DefaultCoverEnterSeconds),
                coverEnterSpeed: Pick("coverEnterSpeed", MotorSpec.DefaultCoverEnterSpeed),
                coverBackHoldSeconds: Pick("coverBackHoldSeconds", MotorSpec.DefaultCoverBackHoldSeconds),
                coverPeekSlideSeconds: Pick("coverPeekSlideSeconds", MotorSpec.DefaultCoverPeekSlideSeconds),
                ladderTopEntryDepth: Pick("ladderTopEntryDepth", MotorSpec.DefaultLadderTopEntryDepth),
                ladderMinApproachDot: Pick("ladderMinApproachDot", MotorSpec.DefaultLadderMinApproachDot),
                ladderMinApproachSpeed: Pick("ladderMinApproachSpeed", MotorSpec.DefaultLadderMinApproachSpeed));
        }

        private static NetMotorSettings Settings(MotorSpec spec)
        {
            return new NetMotorSettings(spec, NetMotorSettings.Default.Stance, CapsuleMoverSpec.Default);
        }

        private static NetMatchFile FileWith(NetMotorSettings motor)
        {
            var file = TestRules.File();
            return new NetMatchFile(
                file.Name, file.Rules, file.Settings, file.LoadoutName, file.LoadoutRule, file.LoadoutWeapons, file.SwitchSeconds,
                file.CatalogName, file.Catalog, motor);
        }

        private static CharacterMotorInput Input(bool cover = false, bool aim = false)
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, false, cover, aim, false, false);
        }

        private CharacterMotorState PressCover(MotorSpec spec)
        {
            var simulation = NewSimulation(spec);
            return simulation.Step(Settle(simulation), Input(cover: true));
        }

        private int PeekTicks(MotorSpec spec)
        {
            _colliders.Clear();
            AddFloor();
            AddBox(new Vector3(-1.5f, 1f, 0.8f + 0.2f), new Vector3(1.6f, 1f, 0.2f));
            var simulation = NewSimulation(spec);
            var state = simulation.Step(Settle(simulation), Input(cover: true));
            Assert.That(state.Cover.IsInCover, Is.True, "the wall 0.8 m ahead is inside the enter distance");
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Cover.Snapshot.Phase, Is.EqualTo(CoverPhase.In));
            var target = Math.Abs(state.Cover.Snapshot.PeekOffset);
            var ticks = 0;
            do
            {
                state = simulation.Step(state, Input(aim: true));
                ticks++;
            }
            while (Math.Abs(state.Cover.AppliedPeek) < spec.CoverPeekSideDistance - 1e-3f && ticks < 600);

            Assert.That(target, Is.EqualTo(0f), "not peeking before aiming");
            Assert.That(state.Cover.Snapshot.Phase, Is.EqualTo(CoverPhase.Peeking));
            return ticks;
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private CharacterMotorSimulation NewSimulation(MotorSpec spec)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(spec), _tick, _killPlaneY) { SpawnPosition = _start };
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
