using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// <see cref="VaultProbe.EvaluateMantle"/> と <see cref="VaultProbe.PlanMantlePath"/>、崖のぼりの調整値 (<see cref="MotorSpec"/>)。
    /// 段は +Z 側にあり、手前の面の法線は -Z。
    /// </summary>
    public sealed class VaultProbeMantleTests
    {
        private const float _tolerance = 1e-4f;
        private const float _radius = 0.5f;
        private const float _distance = 0.8f;
        private const float _vaultMax = MotorSpec.DefaultVaultMaxHeight;
        private const float _mantleMax = MotorSpec.DefaultMantleMaxHeight;
        private const float _speed = MotorSpec.DefaultMantleSpeed;

        private static readonly Vector3 _facing = Vector3.UnitZ;
        private static readonly Vector3 _wallNormal = -Vector3.UnitZ;

        private static VaultObstacleSample Ledge(float height, float depth = float.PositiveInfinity, float distance = _distance)
        {
            return new VaultObstacleSample(distance, _wallNormal, height, depth);
        }

        private static VaultProbeResult Evaluate(VaultObstacleSample sample, Func<VaultPath, bool> isPathClear = null, Vector3? facing = null)
        {
            return VaultProbe.EvaluateMantle(sample, facing ?? _facing, _vaultMax, _mantleMax, _radius, _speed, isPathClear ?? (_ => true));
        }

        [Test]
        public void ADeepLedgeBetweenTheVaultAndMantleHeightsCanBeMantled()
        {
            VaultPath checkedPath = null;

            var result = Evaluate(Ledge(1.8f), path =>
            {
                checkedPath = path;
                return true;
            });

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.Mantle));
            Assert.That(result.CanMantle, Is.True);
            Assert.That(result.CanVault, Is.False);
            Assert.That(result.Height, Is.EqualTo(1.8f));
            Assert.That(result.Direction, Is.EqualTo(Vector3.UnitZ));
            Assert.That(result.Path, Is.Not.Null);
            Assert.That(checkedPath, Is.SameAs(result.Path), "the path that was checked is the one to follow");
        }

        [Test]
        public void TheVaultHeightItselfIsLeftToTheVault()
        {
            Assert.That(Evaluate(Ledge(_vaultMax)).Verdict, Is.EqualTo(VaultVerdict.TooLow));
            Assert.That(Evaluate(Ledge(0.8f)).Verdict, Is.EqualTo(VaultVerdict.TooLow));
            Assert.That(Evaluate(Ledge(_vaultMax + 0.01f)).Verdict, Is.EqualTo(VaultVerdict.Mantle));
        }

        [Test]
        public void TheMantleHeightIsTheLimit()
        {
            Assert.That(Evaluate(Ledge(_mantleMax)).Verdict, Is.EqualTo(VaultVerdict.Mantle));
            Assert.That(Evaluate(Ledge(_mantleMax + 0.01f)).Verdict, Is.EqualTo(VaultVerdict.TooHigh));
        }

        [Test]
        public void ANarrowTopIsNotMantled()
        {
            var footing = VaultProbe.MantleFootingDepth(_radius);
            var pathChecked = false;

            var narrow = Evaluate(Ledge(1.8f, 0.4f), _ =>
            {
                pathChecked = true;
                return true;
            });

            Assert.That(footing, Is.EqualTo((2f * _radius) + VaultProbe.WallGap).Within(_tolerance));
            Assert.That(narrow.Verdict, Is.EqualTo(VaultVerdict.TooNarrow));
            Assert.That(narrow.Path, Is.Null);
            Assert.That(pathChecked, Is.False, "a top too narrow to stand on is refused before checking the path");
            Assert.That(Evaluate(Ledge(1.8f, footing - 0.01f)).Verdict, Is.EqualTo(VaultVerdict.TooNarrow));
            Assert.That(Evaluate(Ledge(1.8f, footing)).Verdict, Is.EqualTo(VaultVerdict.Mantle));
        }

        [Test]
        public void ABlockedHeadroomIsNotMantled()
        {
            var result = Evaluate(Ledge(1.8f), _ => false);

            Assert.That(result.Verdict, Is.EqualTo(VaultVerdict.Blocked));
            Assert.That(result.CanMantle, Is.False);
            Assert.That(result.Path, Is.Not.Null, "the path that was blocked is kept for the log");
        }

        [Test]
        public void TheShapeIsCheckedLikeTheVault()
        {
            Assert.That(Evaluate(VaultObstacleSample.None).Verdict, Is.EqualTo(VaultVerdict.NoObstacle));
            Assert.That(Evaluate(Ledge(1.8f, distance: VaultProbe.MaxReach + 0.1f)).Verdict, Is.EqualTo(VaultVerdict.TooFar));
            Assert.That(
                Evaluate(new VaultObstacleSample(_distance, Vector3.UnitY, 1.8f, float.PositiveInfinity)).Verdict,
                Is.EqualTo(VaultVerdict.NotAWall));
            Assert.That(Evaluate(Ledge(1.8f), facing: Vector3.UnitX).Verdict, Is.EqualTo(VaultVerdict.NotFacing));
        }

        [Test]
        public void ThePathClimbsInFrontOfTheFaceThenStepsOntoTheTop()
        {
            var path = VaultProbe.PlanMantlePath(Vector3.UnitZ, _distance, 1.8f, _radius, _speed);
            var points = path.Points;

            Assert.That(points, Has.Count.EqualTo(4));
            Assert.That(points[0], Is.EqualTo(Vector3.Zero));

            // 上がりきった所で体の表面は面の WallGap 手前、足元は上端の Clearance 上。
            Assert.That(points[1].Z + _radius, Is.EqualTo(_distance - VaultProbe.WallGap).Within(_tolerance));
            Assert.That(points[1].Y, Is.EqualTo(1.8f + VaultProbe.Clearance).Within(_tolerance));

            // 縁の上では体の後ろの端が面の WallGap 奥にあり、足元は上端に下りている。
            Assert.That(points[2].Y, Is.EqualTo(points[1].Y).Within(_tolerance));
            Assert.That(path.End.Z - _radius, Is.EqualTo(_distance + VaultProbe.WallGap).Within(_tolerance));
            Assert.That(path.End.Y, Is.EqualTo(1.8f).Within(_tolerance));
            Assert.That(path.End.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(path.Speed, Is.EqualTo(_speed));
        }

        [Test]
        public void ThePathRisesStraightUpWhenAlreadyAgainstTheFace()
        {
            var path = VaultProbe.PlanMantlePath(Vector3.UnitZ, _radius, 1.5f, _radius, _speed);

            Assert.That(path.Points[1].Z, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(path.Points[1].Y, Is.EqualTo(1.5f + VaultProbe.Clearance).Within(_tolerance));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Func<VaultPath, bool> clear = _ => true;
            var ledge = Ledge(1.8f);

            Assert.Throws<ArgumentNullException>(() => VaultProbe.EvaluateMantle(ledge, _facing, _vaultMax, _mantleMax, _radius, _speed, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.EvaluateMantle(ledge, new Vector3(float.NaN, 0f, 1f), _vaultMax, _mantleMax, _radius, _speed, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.EvaluateMantle(ledge, _facing, -1f, _mantleMax, _radius, _speed, clear));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.EvaluateMantle(ledge, _facing, _vaultMax, float.PositiveInfinity, _radius, _speed, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.EvaluateMantle(ledge, _facing, _vaultMax, _mantleMax, -0.1f, _speed, clear));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.EvaluateMantle(ledge, _facing, _vaultMax, _mantleMax, _radius, 0f, clear));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.EvaluateMantle(Ledge(float.NaN), _facing, _vaultMax, _mantleMax, _radius, _speed, clear));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VaultProbe.EvaluateMantle(Ledge(1.8f, -1f), _facing, _vaultMax, _mantleMax, _radius, _speed, clear));

            Assert.Throws<ArgumentException>(() => VaultProbe.PlanMantlePath(Vector3.UnitY, _distance, 1.8f, _radius, _speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanMantlePath(_facing, -1f, 1.8f, _radius, _speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.PlanMantlePath(_facing, _distance, 1.8f, _radius, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => VaultProbe.MantleFootingDepth(float.NaN));
        }

        [Test]
        public void TheMotorSpecCarriesTheMantleTuningWithDefaults()
        {
            var defaults = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f);
            Assert.That(defaults.MantleMaxHeight, Is.EqualTo(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(defaults.MantleSpeed, Is.EqualTo(MotorSpec.DefaultMantleSpeed));
            Assert.That(MotorSpec.DefaultMantleMaxHeight, Is.GreaterThan(MotorSpec.DefaultVaultMaxHeight), "mantling starts where vaulting stops");

            var tuned = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, mantleMaxHeight: 3f, mantleSpeed: 4f);
            Assert.That(tuned.MantleMaxHeight, Is.EqualTo(3f));
            Assert.That(tuned.MantleSpeed, Is.EqualTo(4f));

            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, mantleMaxHeight: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, mantleMaxHeight: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, mantleSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, mantleSpeed: float.PositiveInfinity));
        }
    }
}
