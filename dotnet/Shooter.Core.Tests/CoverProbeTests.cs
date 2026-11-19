using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CoverProbeTests
    {
        private const float _tolerance = 1e-5f;

        // +Z を向いたプレイヤーの正面にある壁。法線はこちら向き (-Z)。
        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        private static CoverWallSample Sample(
            float distance = 0.8f, float topHeight = 2f, bool wallOnLeft = true, bool wallOnRight = true, Vector3? normal = null)
        {
            return new CoverWallSample(distance, normal ?? _wallNormal, topHeight, wallOnLeft, wallOnRight);
        }

        private static Vector3 Facing(float yawDegrees)
        {
            var radians = yawDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        [Test]
        public void NothingInFrontIsNotAWall()
        {
            var result = CoverProbe.Evaluate(CoverWallSample.None, _forward, MotorSpec.Default);

            Assert.That(result.IsWall, Is.False);
            Assert.That(result.CanEnter, Is.False);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(4.0f)]
        public void EntersWithinTheDefaultFourMetres(float distance)
        {
            var result = CoverProbe.Evaluate(Sample(distance), _forward, MotorSpec.Default);

            Assert.That(result.IsWall, Is.True);
            Assert.That(result.CanEnter, Is.True);
            Assert.That(result.Distance, Is.EqualTo(distance));
        }

        [TestCase(4.01f)]
        [TestCase(6f)]
        public void WallsFartherThanFourMetresAreOutOfReach(float distance)
        {
            var result = CoverProbe.Evaluate(Sample(distance), _forward, MotorSpec.Default);

            Assert.That(result.IsWall, Is.False);
            Assert.That(result.CanEnter, Is.False);
        }

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(45f)]
        [TestCase(-45f)]
        public void EntersWhenFacingWithin45Degrees(float yaw)
        {
            Assert.That(CoverProbe.Evaluate(Sample(), Facing(yaw), MotorSpec.Default).CanEnter, Is.True);
        }

        [TestCase(46f)]
        [TestCase(-46f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void DoesNotEnterWhenFacingAwayMoreThan45Degrees(float yaw)
        {
            var result = CoverProbe.Evaluate(Sample(), Facing(yaw), MotorSpec.Default);

            Assert.That(result.CanEnter, Is.False);
            Assert.That(result.IsWall, Is.True, "the wall is still there, only the angle is off");
        }

        // 壁の法線は -Z なので壁に沿った右は +X。右へ 40° 向いて 3 m 先に当たると、当たり点は体の 3 tan 40° 右にあり、体は左 (負) へずれている。
        [Test]
        public void EnterSideOffsetIsHowFarTheBodyIsBesideTheHitPoint()
        {
            var normal = new Vector3(0f, 0f, -1f);

            Assert.That(CoverProbe.EnterSideOffset(Facing(0f), normal, 3f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(CoverProbe.EnterSideOffset(Facing(40f), normal, 3f / (float)Math.Cos(40.0 * Math.PI / 180.0)), Is.EqualTo(-3f * (float)Math.Tan(40.0 * Math.PI / 180.0)).Within(1e-4f));
            Assert.That(CoverProbe.EnterSideOffset(Facing(-30f), normal, 2f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(CoverProbe.EnterSideOffset(Vector3.UnitY, normal, 2f), Is.EqualTo(0f));
        }

        [Test]
        public void IgnoresThePitchOfTheFacing()
        {
            var lookingDown = new Vector3(0f, -0.9f, 0.1f);

            Assert.That(CoverProbe.Evaluate(Sample(), lookingDown, MotorSpec.Default).CanEnter, Is.True);
        }

        [Test]
        public void StraightUpOrDownFacingCannotEnter()
        {
            Assert.That(CoverProbe.Evaluate(Sample(), Vector3.UnitY, MotorSpec.Default).CanEnter, Is.False);
        }

        [Test]
        public void FloorsAndSteepSlopesAreNotWalls()
        {
            Assert.That(CoverProbe.Evaluate(Sample(normal: Vector3.UnitY), _forward, MotorSpec.Default).IsWall, Is.False);
            Assert.That(CoverProbe.Evaluate(Sample(normal: new Vector3(0f, 0.5f, -0.866f)), _forward, MotorSpec.Default).IsWall, Is.False);
            Assert.That(CoverProbe.Evaluate(Sample(normal: Vector3.Zero), _forward, MotorSpec.Default).IsWall, Is.False);
        }

        [Test]
        public void SlightlyTiltedWallIsAWallAndItsNormalIsFlattened()
        {
            var result = CoverProbe.Evaluate(Sample(normal: new Vector3(0f, 0.2f, -0.98f)), _forward, MotorSpec.Default);

            Assert.That(result.IsWall, Is.True);
            Assert.That(result.Normal.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(result.Normal.Y, Is.EqualTo(0f));
            Assert.That(result.Normal.Z, Is.EqualTo(-1f).Within(_tolerance));
        }

        [TestCase(0.3f, CoverHeight.Low)]
        [TestCase(1.0f, CoverHeight.Low)]
        [TestCase(1.09f, CoverHeight.Low)]
        [TestCase(1.1f, CoverHeight.High)]
        [TestCase(2.0f, CoverHeight.High)]
        public void WallsBelow1Point1MetresAreLow(float topHeight, CoverHeight expected)
        {
            Assert.That(CoverProbe.Evaluate(Sample(topHeight: topHeight), _forward, MotorSpec.Default).Height, Is.EqualTo(expected));
        }

        [TestCase(true, true, CoverEdge.None)]
        [TestCase(false, true, CoverEdge.Left)]
        [TestCase(true, false, CoverEdge.Right)]
        [TestCase(false, false, CoverEdge.Left | CoverEdge.Right)]
        public void MissingWallBesideIsAnEdge(bool wallOnLeft, bool wallOnRight, CoverEdge expected)
        {
            var result = CoverProbe.Evaluate(Sample(wallOnLeft: wallOnLeft, wallOnRight: wallOnRight), _forward, MotorSpec.Default);

            Assert.That(result.Edges, Is.EqualTo(expected));
            Assert.That(result.IsAtLeftEdge, Is.EqualTo(!wallOnLeft));
            Assert.That(result.IsAtRightEdge, Is.EqualTo(!wallOnRight));
        }

        [Test]
        public void LowWallsPeekUpWithoutMovingSideways()
        {
            var result = CoverProbe.Evaluate(Sample(topHeight: 1f, wallOnLeft: false), _forward, MotorSpec.Default);

            Assert.That(CoverProbe.PeekOffset(result, 1f, MotorSpec.Default), Is.EqualTo(0f));
        }

        [Test]
        public void HighWallsAwayFromAnEdgeHaveNoPeek()
        {
            Assert.That(CoverProbe.PeekOffset(CoverProbe.Evaluate(Sample(), _forward, MotorSpec.Default), 1f, MotorSpec.Default), Is.EqualTo(0f));
        }

        [Test]
        public void HighWallsPeekSidewaysOutOfTheOpenEdge()
        {
            var left = CoverProbe.Evaluate(Sample(wallOnLeft: false), _forward, MotorSpec.Default);
            var right = CoverProbe.Evaluate(Sample(wallOnRight: false), _forward, MotorSpec.Default);

            Assert.That(CoverProbe.PeekOffset(left, 1f, MotorSpec.Default), Is.EqualTo(-MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(CoverProbe.PeekOffset(right, -1f, MotorSpec.Default), Is.EqualTo(MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(MotorSpec.DefaultCoverPeekSideDistance, Is.EqualTo(0.6f));
        }

        [Test]
        public void NarrowPillarsPeekToThePreferredSide()
        {
            var pillar = CoverProbe.Evaluate(Sample(wallOnLeft: false, wallOnRight: false), _forward, MotorSpec.Default);

            Assert.That(CoverProbe.PeekOffset(pillar, -1f, MotorSpec.Default), Is.EqualTo(-MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(CoverProbe.PeekOffset(pillar, 1f, MotorSpec.Default), Is.EqualTo(MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(CoverProbe.PeekOffset(pillar, 0f, MotorSpec.Default), Is.EqualTo(MotorSpec.DefaultCoverPeekSideDistance));
        }

        [Test]
        public void NoWallHasNoPeek()
        {
            Assert.That(CoverProbe.PeekOffset(CoverProbeResult.None, 1f, MotorSpec.Default), Is.EqualTo(0f));
        }

        [Test]
        public void HoldDistanceIsTheRadiusPlusTheGap()
        {
            Assert.That(CoverProbe.HoldDistance(0.5f), Is.EqualTo(0.55f).Within(_tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoverProbe.HoldDistance(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoverProbe.HoldDistance(float.NaN));
        }

        [Test]
        public void RightAlongIsTheRightHandWhenFacingTheWall()
        {
            var right = CoverProbe.RightAlong(_wallNormal);

            Assert.That(right.X, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(right.Y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(right.Z, Is.EqualTo(0f).Within(_tolerance));

            // +X を向いて (法線は -X) 右手は -Z。
            var turned = CoverProbe.RightAlong(new Vector3(-1f, 0f, 0f));
            Assert.That(turned.Z, Is.EqualTo(-1f).Within(_tolerance));
        }

        [Test]
        public void RightAlongRejectsVerticalNormals()
        {
            Assert.Throws<ArgumentException>(() => CoverProbe.RightAlong(Vector3.UnitY));
        }

        [Test]
        public void RejectsNonFiniteSamples()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CoverProbe.Evaluate(Sample(float.NaN), _forward, MotorSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoverProbe.Evaluate(Sample(topHeight: float.PositiveInfinity), _forward, MotorSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CoverProbe.Evaluate(Sample(normal: new Vector3(float.NaN, 0f, -1f)), _forward, MotorSpec.Default));
        }
    }
}
