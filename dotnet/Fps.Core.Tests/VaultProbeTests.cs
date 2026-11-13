using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class VaultProbeTests
    {
        private const float _tolerance = 1e-4f;
        private const float _radius = 0.5f;

        // +Z を向いたプレイヤーの正面にある障害物。法線はこちら向き (-Z)。
        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        private static VaultObstacleSample Sample(float distance = 0.8f, float topHeight = 1f, float depth = 0.4f, Vector3? normal = null)
        {
            return new VaultObstacleSample(distance, normal ?? _wallNormal, topHeight, depth);
        }

        private static VaultProbeResult Evaluate(VaultObstacleSample sample, Vector3? facing = null, bool clear = true)
        {
            return VaultProbe.Evaluate(sample, facing ?? _forward, MotorSpec.DefaultVaultMaxHeight, _radius, _ => clear);
        }

        private static Vector3 Facing(float yawDegrees)
        {
            var radians = yawDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        [Test]
        public void NothingInFrontIsNoObstacle()
        {
            var called = false;
            var result = VaultProbe.Evaluate(VaultObstacleSample.None, _forward, 1.2f, _radius, _ => called = true);

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.NoObstacle));
            Assert.That(result.CanVault, Is.False);
            Assert.That(result.Path, Is.Null);
            Assert.That(called, Is.False, "nothing to check without an obstacle");
        }

        [Test]
        public void VaultsALowThinWallInFront()
        {
            var result = Evaluate(Sample());

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.Vault));
            Assert.That(result.CanVault, Is.True);
            Assert.That(result.Height, Is.EqualTo(1f));
            Assert.That(result.Depth, Is.EqualTo(0.4f));
            Assert.That(result.Direction, Is.EqualTo(_forward));
            Assert.That(result.Path, Is.Not.Null);
        }

        [TestCase(0.3f)]
        [TestCase(1f)]
        [TestCase(1.2f)]
        public void VaultsUpToTheMaxHeight(float height)
        {
            Assert.That(Evaluate(Sample(topHeight: height)).Verdict, Is.EqualTo(VaultVerdict.Vault));
        }

        [TestCase(1.21f)]
        [TestCase(2f)]
        public void WallsHigherThanTheMaxHeightAreJumpedInstead(float height)
        {
            var result = Evaluate(Sample(topHeight: height));

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.TooHigh));
            Assert.That(result.CanVault, Is.False);
            Assert.That(result.Path, Is.Null);
        }

        [Test]
        public void UsesTheGivenMaxHeight()
        {
            var sample = Sample(topHeight: 1.4f);

            Assert.That(VaultProbe.Evaluate(sample, _forward, 1.2f, _radius, _ => true).Verdict, Is.EqualTo(VaultVerdict.TooHigh));
            Assert.That(VaultProbe.Evaluate(sample, _forward, 1.5f, _radius, _ => true).Verdict, Is.EqualTo(VaultVerdict.Vault));
        }

        [TestCase(0f)]
        [TestCase(0.29f)]
        public void StepsLowerThanTheMinHeightAreNotVaulted(float height)
        {
            Assert.That(Evaluate(Sample(topHeight: height)).Verdict, Is.EqualTo(VaultVerdict.TooLow));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void VaultsWithinReach(float distance)
        {
            Assert.That(Evaluate(Sample(distance: distance)).Verdict, Is.EqualTo(VaultVerdict.Vault));
        }

        [Test]
        public void ObstaclesFartherThanTheReachAreTooFar()
        {
            Assert.That(Evaluate(Sample(distance: 1.01f)).Verdict, Is.EqualTo(VaultVerdict.TooFar));
        }

        [TestCase(1f)]
        [TestCase(1.01f)]
        [TestCase(float.PositiveInfinity)]
        public void ThickObstaclesUpToTheMaxDepthOnly(float depth)
        {
            var expected = depth <= VaultProbe.MaxDepth ? VaultVerdict.Vault : VaultVerdict.TooDeep;

            Assert.That(Evaluate(Sample(depth: depth)).Verdict, Is.EqualTo(expected));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(-45f)]
        public void VaultsWhenFacingWithin45Degrees(float yaw)
        {
            Assert.That(Evaluate(Sample(), Facing(yaw)).Verdict, Is.EqualTo(VaultVerdict.Vault));
        }

        [TestCase(46f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void DoesNotVaultWhenFacingAway(float yaw)
        {
            Assert.That(Evaluate(Sample(), Facing(yaw)).Verdict, Is.EqualTo(VaultVerdict.NotFacing));
        }

        [Test]
        public void IgnoresThePitchOfTheFacing()
        {
            Assert.That(Evaluate(Sample(), new Vector3(0f, -5f, 1f)).Verdict, Is.EqualTo(VaultVerdict.Vault));
            Assert.That(Evaluate(Sample(), Vector3.UnitY).Verdict, Is.EqualTo(VaultVerdict.NotFacing), "looking straight up has no direction");
        }

        [Test]
        public void FloorsAndSteepSlopesAreNotWalls()
        {
            Assert.That(Evaluate(Sample(normal: Vector3.UnitY)).Verdict, Is.EqualTo(VaultVerdict.NotAWall));
            Assert.That(Evaluate(Sample(normal: new Vector3(0f, 0.5f, -0.8f))).Verdict, Is.EqualTo(VaultVerdict.NotAWall));
            Assert.That(Evaluate(Sample(normal: Vector3.Zero)).Verdict, Is.EqualTo(VaultVerdict.NotAWall));
        }

        [Test]
        public void ABlockedPathIsNotVaulted()
        {
            var result = Evaluate(Sample(), clear: false);

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.Blocked));
            Assert.That(result.CanVault, Is.False);
            Assert.That(result.Path, Is.Not.Null, "the path that was checked");
        }

        [Test]
        public void ChecksThePlannedPath()
        {
            VaultPath checkedPath = null;
            var result = VaultProbe.Evaluate(Sample(), _forward, 1.2f, _radius, path =>
            {
                checkedPath = path;
                return true;
            });

            Assert.That(checkedPath, Is.SameAs(result.Path));
        }

        [Test]
        public void DoesNotCheckThePathWhenTheShapeAlreadyFails()
        {
            var called = false;
            VaultProbe.Evaluate(Sample(topHeight: 2f), _forward, 1.2f, _radius, _ => called = true);

            Assert.That(called, Is.False);
        }

        [Test]
        public void PathRisesAboveTheTopCrossesAndLandsBeyondTheFarSide()
        {
            var path = Evaluate(Sample(distance: 0.8f, topHeight: 1f, depth: 0.4f)).Path;

            var rise = 1f + VaultProbe.Clearance;
            var approach = 0.8f - _radius - VaultProbe.WallGap;
            var landing = 0.8f + 0.4f + _radius + VaultProbe.WallGap;
            Assert.That(path.Points, Has.Count.EqualTo(4));
            AssertNear(path.Points[0], Vector3.Zero);
            AssertNear(path.Points[1], new Vector3(0f, rise, approach));
            AssertNear(path.Points[2], new Vector3(0f, rise, landing));
            AssertNear(path.Points[3], new Vector3(0f, 0f, landing));
            Assert.That(path.Speed, Is.EqualTo(VaultProbe.Speed));
        }

        [Test]
        public void PathGoesStraightUpWhenAlreadyAgainstTheWall()
        {
            var path = VaultProbe.PlanPath(_forward, 0.5f, 1f, 0.4f, _radius);

            Assert.That(path.Points[1].Z, Is.EqualTo(0f));
        }

        [Test]
        public void PathFollowsTheWallNormalNotTheFacing()
        {
            var normal = Vector3.Normalize(new Vector3(-1f, 0f, -1f));
            var result = Evaluate(Sample(normal: normal), Facing(30f));

            Assert.That(result.CanVault, Is.True);
            var end = result.Path.End;
            Assert.That(end.X, Is.EqualTo(end.Z).Within(_tolerance), "perpendicular to the wall");
            Assert.That(end.Y, Is.EqualTo(0f));
            Assert.That(result.Direction.X, Is.EqualTo(result.Direction.Z).Within(_tolerance));
        }

        [Test]
        public void EvaluateRejectsInvalidArguments()
        {
            Func<VaultPath, bool> clear = _ => true;

            Assert.Throws<ArgumentNullException>(() => VaultProbe.Evaluate(Sample(), _forward, 1.2f, _radius, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.Evaluate(Sample(), _forward, -1f, _radius, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.Evaluate(Sample(), _forward, float.NaN, _radius, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.Evaluate(Sample(), _forward, 1.2f, -0.1f, clear));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.Evaluate(Sample(), new Vector3(float.NaN, 0f, 1f), 1.2f, _radius, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(distance: float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(topHeight: float.PositiveInfinity)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(depth: float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(depth: -0.1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(distance: -0.1f)));
        }

        [Test]
        public void PlanPathRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => VaultProbe.PlanPath(Vector3.UnitY, 0.8f, 1f, 0.4f, _radius));
            Assert.Throws<ArgumentException>(() => VaultProbe.PlanPath(new Vector3(float.NaN, 0f, 1f), 0.8f, 1f, 0.4f, _radius));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, -1f, 1f, 0.4f, _radius));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, float.NaN, 0.4f, _radius));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, float.PositiveInfinity, _radius));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, 0.4f, -1f));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance), "X");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance), "Y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance), "Z");
        }
    }
}
