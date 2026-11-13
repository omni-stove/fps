using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SpreadPatternTests
    {
        private const string _id = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";

        private static WeaponSpec Spec(float hip = 0f, float ads = 0f, float move = 0f)
        {
            return new WeaponSpec(
                _id, "Test", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 10, 100f, DamageFalloff.None,
                hipSpreadDegrees: hip, adsSpreadDegrees: ads, moveSpreadDegrees: move);
        }

        private static float AngleDegrees(Vector3 a, Vector3 b)
        {
            var cos = Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b));
            return (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI);
        }

        [Test]
        public void ConeIsHipSpreadWhenNotAimingAndStill()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 6f, ads: 1f, move: 4f), 0f, 0f), Is.EqualTo(6f));
        }

        [Test]
        public void ConeIsAdsSpreadWhenFullyAiming()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 6f, ads: 1f, move: 4f), 1f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void ConeBlendsBetweenHipAndAdsByProgress()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 6f, ads: 2f), 0.25f, 0f), Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void ConeAddsMoveSpreadByMoveFraction()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 6f, ads: 1f, move: 4f), 0f, 0.5f), Is.EqualTo(8f).Within(1e-5f));
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 6f, ads: 1f, move: 4f), 1f, 1f), Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void ConeClampsInputsOutsideZeroToOne()
        {
            var spec = Spec(hip: 6f, ads: 1f, move: 4f);

            Assert.That(SpreadPattern.ConeDegrees(spec, -1f, -1f), Is.EqualTo(6f));
            Assert.That(SpreadPattern.ConeDegrees(spec, 2f, 3f), Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void ConeDoesNotExceedTheMaximum()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(hip: 80f, move: 80f), 0f, 1f), Is.EqualTo(SpreadPattern.MaxDegrees));
        }

        [Test]
        public void ConeIsZeroForAWeaponWithoutSpread()
        {
            Assert.That(SpreadPattern.ConeDegrees(Spec(), 0.3f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void ConeRejectsNullSpecAndNaN()
        {
            Assert.Throws<ArgumentNullException>(() => SpreadPattern.ConeDegrees(null, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.ConeDegrees(Spec(), float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.ConeDegrees(Spec(), 0f, float.NaN));
        }

        [Test]
        public void MoveFractionIsTheShareOfFullSpeed()
        {
            Assert.That(SpreadPattern.MoveFraction(0f, 5f), Is.EqualTo(0f));
            Assert.That(SpreadPattern.MoveFraction(2.5f, 5f), Is.EqualTo(0.5f));
            Assert.That(SpreadPattern.MoveFraction(8f, 5f), Is.EqualTo(1f));
            Assert.That(SpreadPattern.MoveFraction(-1f, 5f), Is.EqualTo(0f));
        }

        [Test]
        public void MoveFractionIsOneWhenMovingWithoutAFullSpeed()
        {
            Assert.That(SpreadPattern.MoveFraction(0.1f, 0f), Is.EqualTo(1f));
            Assert.That(SpreadPattern.MoveFraction(0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void MoveFractionRejectsNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.MoveFraction(float.NaN, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.MoveFraction(1f, float.PositiveInfinity));
        }

        [Test]
        public void ZeroConeReturnsTheNormalizedAim()
        {
            var direction = SpreadPattern.Direction(new Vector3(0f, 0f, 5f), 0f, 1, 7);

            Assert.That(direction.X, Is.EqualTo(0f));
            Assert.That(direction.Y, Is.EqualTo(0f));
            Assert.That(direction.Z, Is.EqualTo(1f));
        }

        [Test]
        public void SameSeedAlwaysGivesTheSameDirection()
        {
            var aim = new Vector3(0.3f, -0.2f, 1f);

            for (var shot = 0; shot < 50; shot++)
            {
                var first = SpreadPattern.Direction(aim, 10f, 42, shot);
                var second = SpreadPattern.Direction(aim, 10f, 42, shot);

                Assert.That(second, Is.EqualTo(first));
            }
        }

        [Test]
        public void DifferentShotsOrPlayersGiveDifferentDirections()
        {
            var aim = Vector3.UnitZ;
            var baseline = SpreadPattern.Direction(aim, 10f, 1, 0);

            Assert.That(SpreadPattern.Direction(aim, 10f, 1, 1), Is.Not.EqualTo(baseline));
            Assert.That(SpreadPattern.Direction(aim, 10f, 2, 0), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void DirectionsStayInsideTheConeAndHaveUnitLength()
        {
            var aim = new Vector3(1f, 0.5f, -2f);

            for (var shot = 0; shot < 500; shot++)
            {
                var direction = SpreadPattern.Direction(aim, 8f, 3, shot);

                Assert.That(direction.Length(), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(AngleDegrees(aim, direction), Is.LessThanOrEqualTo(8f + 1e-3f));
            }
        }

        [Test]
        public void DirectionsSpreadAroundTheAim()
        {
            // 種を連番で変えても、向きが片側に寄らずに円錐の周り全体へ散る。
            var aim = Vector3.UnitZ;
            var left = 0;
            var right = 0;
            var up = 0;
            var down = 0;
            var sum = Vector3.Zero;

            for (var shot = 0; shot < 1000; shot++)
            {
                var direction = SpreadPattern.Direction(aim, 10f, 5, shot);
                sum += direction;
                left += direction.X < 0f ? 1 : 0;
                right += direction.X > 0f ? 1 : 0;
                down += direction.Y < 0f ? 1 : 0;
                up += direction.Y > 0f ? 1 : 0;
            }

            Assert.That(left, Is.GreaterThan(400));
            Assert.That(right, Is.GreaterThan(400));
            Assert.That(up, Is.GreaterThan(400));
            Assert.That(down, Is.GreaterThan(400));
            Assert.That(AngleDegrees(aim, sum), Is.LessThan(1f));
        }

        [Test]
        public void DirectionWorksWhenAimingStraightUp()
        {
            var direction = SpreadPattern.Direction(Vector3.UnitY, 5f, 1, 3);

            Assert.That(AngleDegrees(Vector3.UnitY, direction), Is.LessThanOrEqualTo(5f + 1e-3f));
        }

        [Test]
        public void DirectionRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.Direction(Vector3.Zero, 5f, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.Direction(new Vector3(float.NaN, 0f, 1f), 5f, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.Direction(Vector3.UnitZ, -1f, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.Direction(Vector3.UnitZ, 91f, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpreadPattern.Direction(Vector3.UnitZ, float.NaN, 1, 0));
        }

        [Test]
        public void SampleIsInUnitRangeAndDependsOnTheStream()
        {
            for (var shot = -10; shot < 100; shot++)
            {
                var polar = SpreadPattern.Sample(-3, shot, SpreadPattern.PolarStream);
                var azimuth = SpreadPattern.Sample(-3, shot, SpreadPattern.AzimuthStream);

                Assert.That(polar, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                Assert.That(azimuth, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                Assert.That(azimuth, Is.Not.EqualTo(polar));
            }
        }
    }
}
