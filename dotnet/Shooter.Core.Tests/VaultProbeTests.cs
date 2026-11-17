using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
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
            return VaultProbe.Evaluate(sample, facing ?? _forward, MotorSpec.Default, _radius, _ => clear);
        }

        private static MotorSpec VaultHeight(float vaultMaxHeight)
        {
            return new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, vaultMaxHeight: vaultMaxHeight);
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
            var result = VaultProbe.Evaluate(VaultObstacleSample.None, _forward, MotorSpec.Default, _radius, _ => called = true);

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

            Assert.That(VaultProbe.Evaluate(sample, _forward, MotorSpec.Default, _radius, _ => true).Verdict, Is.EqualTo(VaultVerdict.TooHigh));
            Assert.That(VaultProbe.Evaluate(sample, _forward, VaultHeight(1.5f), _radius, _ => true).Verdict, Is.EqualTo(VaultVerdict.Vault));
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

        [TestCase(1f, VaultVerdict.Vault)]
        [TestCase(1.01f, VaultVerdict.TooDeep)]
        [TestCase(1.09f, VaultVerdict.TooDeep)]
        [TestCase(1.1f, VaultVerdict.Vault)]
        [TestCase(float.PositiveInfinity, VaultVerdict.Vault)]
        public void ObstaclesThickerThanTheMaxDepthAreClimbedOntoOnlyWhenTheBodyFitsOnTop(float depth, VaultVerdict expected)
        {
            Assert.That(Evaluate(Sample(depth: depth)).Verdict, Is.EqualTo(expected));
        }

        [TestCase(0f, 1)]
        [TestCase(0.35f, 1)]
        [TestCase(0.45f, 1)]
        [TestCase(0.46f, 2)]
        [TestCase(1.2f, 9)]
        [TestCase(2.2f, 19)]
        [TestCase(100f, VaultProbe.MaxProbeHeights)]
        public void ProbeHeightsStayBelowTheMaxHeight(float maxHeight, int expected)
        {
            var count = VaultProbe.ProbeHeightCount(maxHeight);

            Assert.That(count, Is.EqualTo(expected));
            Assert.That(VaultProbe.ProbeHeightAt(0), Is.EqualTo(VaultProbe.LowestProbeHeight));
            if (count > 1)
            {
                Assert.That(VaultProbe.ProbeHeightAt(count - 1), Is.LessThan(maxHeight));
            }
        }

        [Test]
        public void ProbeHeightsRejectBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.ProbeHeightCount(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.ProbeHeightCount(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.ProbeHeightAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.ProbeHeightAt(VaultProbe.MaxProbeHeights));
            Assert.That(VaultProbe.ProbeHeightAt(8), Is.EqualTo(1.15f).Within(_tolerance));
        }

        [Test]
        public void TheNearestWallIsPreferredOverFloorsAndUndersides()
        {
            var wall = _wallNormal;
            var underside = new Vector3(0f, -1f, 0f);

            Assert.That(VaultProbe.IsWall(wall), Is.True);
            Assert.That(VaultProbe.IsWall(underside), Is.False);
            Assert.That(VaultProbe.IsWall(Vector3.Zero), Is.False);
            Assert.That(VaultProbe.IsBetterFace(false, Vector3.Zero, 0f, underside, 0.5f), Is.True, "anything beats nothing");
            Assert.That(VaultProbe.IsBetterFace(true, wall, 0.9f, wall, 0.6f), Is.True, "the nearer wall");
            Assert.That(VaultProbe.IsBetterFace(true, wall, 0.6f, wall, 0.9f), Is.False);
            Assert.That(VaultProbe.IsBetterFace(true, wall, 0.9f, underside, 0.2f), Is.False, "a wall beats a nearer underside");
            Assert.That(VaultProbe.IsBetterFace(true, underside, 0.2f, wall, 0.9f), Is.True);
        }

        [TestCase(0.99f, VaultVerdict.NarrowFace)]
        [TestCase(1f, VaultVerdict.Vault)]
        [TestCase(float.PositiveInfinity, VaultVerdict.Vault)]
        public void TheFaceMustBeAsWideAsTheBody(float width, VaultVerdict expected)
        {
            var sample = new VaultObstacleSample(0.8f, _wallNormal, 1f, 0.4f, width);

            Assert.That(Evaluate(sample).Verdict, Is.EqualTo(expected));
        }

        [Test]
        public void TheEndOfAThinWallIsNotClimbedOntoFromTheSide()
        {
            // 横から当たった壁の端の面: 幅は壁の厚さ、奥行きは壁の長さで測れないほど深い。
            var sample = new VaultObstacleSample(0.8f, _wallNormal, 1f, float.PositiveInfinity, 0.4f);

            Assert.That(Evaluate(sample).Verdict, Is.EqualTo(VaultVerdict.NarrowFace));
        }

        [Test]
        public void AMissingWidthIsTreatedAsWideAndANegativeOneIsRejected()
        {
            Assert.That(new VaultObstacleSample(0.8f, _wallNormal, 1f, 0.4f).Width, Is.EqualTo(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(new VaultObstacleSample(0.8f, _wallNormal, 1f, 0.4f, -1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(new VaultObstacleSample(0.8f, _wallNormal, 1f, 0.4f, float.NaN)));
        }

        [Test]
        public void FaceWidthProbesStepOutToTheBodyRadius()
        {
            Assert.That(VaultProbe.MinFaceWidth(0.5f), Is.EqualTo(1f));
            Assert.That(VaultProbe.FaceWidthProbeCount(0.5f), Is.EqualTo(5));
            Assert.That(VaultProbe.FaceWidthOffset(0.5f, 0), Is.EqualTo(0.1f).Within(_tolerance));
            Assert.That(VaultProbe.FaceWidthOffset(0.5f, 4), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(VaultProbe.FaceWidthProbeCount(0.45f), Is.EqualTo(5));
            Assert.That(VaultProbe.FaceWidthOffset(0.45f, 4), Is.EqualTo(0.45f).Within(_tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.FaceWidthOffset(0.5f, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.MinFaceWidth(-1f));
        }

        [Test]
        public void MeasuringTheFaceAddsHowFarItGoesOnEachSideUpToTheRadius()
        {
            // +Z を向いて x = -0.3〜2 の面に当たっている。
            var width = VaultProbe.MeasureFaceWidth(new Vector3(0f, 0.5f, 0.8f), _forward, _radius, FaceBetween(-0.3f, 2f, _wallNormal));

            Assert.That(width, Is.EqualTo(0.8f).Within(_tolerance));
            Assert.That(
                VaultProbe.MeasureFaceWidth(new Vector3(0f, 0.5f, 0.8f), _forward, _radius, FaceBetween(-5f, 5f, _wallNormal)),
                Is.EqualTo(1f).Within(_tolerance));
            Assert.That(
                VaultProbe.MeasureFaceWidth(new Vector3(0f, 0.5f, 0.8f), _forward, _radius, FaceBetween(-5f, 5f, Vector3.UnitY)),
                Is.EqualTo(0f),
                "a floor is not a face");
        }

        [Test]
        public void MeasuringTheFaceRejectsBadArguments()
        {
            var face = FaceBetween(-5f, 5f, _wallNormal);

            Assert.Throws<ArgumentNullException>(() => VaultProbe.MeasureFaceWidth(Vector3.Zero, _forward, _radius, null));
            Assert.Throws<ArgumentException>(() => VaultProbe.MeasureFaceWidth(Vector3.Zero, Vector3.UnitY, _radius, face));
        }

        // x が from〜to の間で飛ばした Ray だけが、面の手前 FaceWidthBack の所で normal の面に当たる。
        private static VaultProbe.FaceRaycast FaceBetween(float from, float to, Vector3 normal)
        {
            return (Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitNormal, out float distance) =>
            {
                var inside = origin.X >= from - _tolerance && origin.X <= to + _tolerance;
                hitNormal = inside ? normal : Vector3.Zero;
                distance = inside ? VaultProbe.FaceWidthBack : 0f;
                return inside;
            };
        }

        [Test]
        public void AThickLowPlatformIsClimbedOntoAndTheBodyEndsOnTop()
        {
            var result = Evaluate(Sample(distance: 0.8f, topHeight: 0.8f, depth: float.PositiveInfinity));

            Assert.That(result.CanVault, Is.True);
            var points = result.Path.Points;
            Assert.That(points.Count, Is.EqualTo(4));
            AssertNear(points[1], new Vector3(0f, 0.95f, 0.2f));
            AssertNear(points[2], new Vector3(0f, 0.95f, 1.4f));
            AssertNear(points[3], new Vector3(0f, 0.8f, 1.4f));
            Assert.That(result.Path.Speed, Is.EqualTo(MotorSpec.DefaultVaultSpeed));
        }

        [Test]
        public void AThickPlatformWithNoRoomOnTopIsBlocked()
        {
            Assert.That(Evaluate(Sample(depth: float.PositiveInfinity), clear: false).Verdict, Is.EqualTo(VaultVerdict.Blocked));
        }

        [Test]
        public void AThickPlatformAboveTheMaxHeightIsStillTooHigh()
        {
            Assert.That(Evaluate(Sample(topHeight: 1.5f, depth: float.PositiveInfinity)).Verdict, Is.EqualTo(VaultVerdict.TooHigh));
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
            var result = VaultProbe.Evaluate(Sample(), _forward, MotorSpec.Default, _radius, path =>
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
            VaultProbe.Evaluate(Sample(topHeight: 2f), _forward, MotorSpec.Default, _radius, _ => called = true);

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
            Assert.That(path.Speed, Is.EqualTo(MotorSpec.DefaultVaultSpeed));
        }

        [Test]
        public void PathGoesStraightUpWhenAlreadyAgainstTheWall()
        {
            var path = VaultProbe.PlanPath(_forward, 0.5f, 1f, 0.4f, _radius, MotorSpec.DefaultVaultSpeed);

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

            Assert.Throws<ArgumentNullException>(() => VaultProbe.Evaluate(Sample(), _forward, MotorSpec.Default, _radius, null));
            Assert.Throws<ArgumentNullException>(() => VaultProbe.Evaluate(Sample(), _forward, null, _radius, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.Evaluate(Sample(), _forward, MotorSpec.Default, -0.1f, clear));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.Evaluate(Sample(), new Vector3(float.NaN, 0f, 1f), MotorSpec.Default, _radius, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(distance: float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(topHeight: float.PositiveInfinity)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(depth: float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(depth: -0.1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(Sample(distance: -0.1f)));
        }

        [Test]
        public void PlanPathRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => VaultProbe.PlanPath(Vector3.UnitY, 0.8f, 1f, 0.4f, _radius, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentException>(() => VaultProbe.PlanPath(new Vector3(float.NaN, 0f, 1f), 0.8f, 1f, 0.4f, _radius, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, -1f, 1f, 0.4f, _radius, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, float.NaN, 0.4f, _radius, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, float.PositiveInfinity, _radius, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, 0.4f, -1f, MotorSpec.DefaultVaultSpeed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, 0.4f, _radius, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanPath(_forward, 0.8f, 1f, 0.4f, _radius, float.PositiveInfinity));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance), "X");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance), "Y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance), "Z");
        }
    }
}
