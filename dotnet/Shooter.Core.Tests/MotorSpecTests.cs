using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MotorSpecTests
    {
        private static MotorSpec Spec(
            float safeFallSpeed = MotorSpec.DefaultSafeFallSpeed,
            float lethalFallSpeed = MotorSpec.DefaultLethalFallSpeed,
            float dodgeDistance = MotorSpec.DefaultDodgeDistance,
            float dodgeSeconds = MotorSpec.DefaultDodgeSeconds,
            float vaultMaxHeight = MotorSpec.DefaultVaultMaxHeight)
        {
            return new MotorSpec(
                4f, 8f, 1.5f, 9.81f, 0.5f, 2f,
                safeFallSpeed, lethalFallSpeed, dodgeDistance, dodgeSeconds, vaultMaxHeight);
        }

        [Test]
        public void NewValuesUseTheDefaultsWhenOmitted()
        {
            var spec = new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f);

            Assert.That(spec.SafeFallSpeed, Is.EqualTo(MotorSpec.DefaultSafeFallSpeed));
            Assert.That(spec.LethalFallSpeed, Is.EqualTo(MotorSpec.DefaultLethalFallSpeed));
            Assert.That(spec.DodgeDistance, Is.EqualTo(MotorSpec.DefaultDodgeDistance));
            Assert.That(spec.DodgeSeconds, Is.EqualTo(MotorSpec.DefaultDodgeSeconds));
            Assert.That(spec.VaultMaxHeight, Is.EqualTo(MotorSpec.DefaultVaultMaxHeight));
        }

        [Test]
        public void LeanAndAirJumpUseTheDefaultsWhenOmitted()
        {
            var spec = new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f);

            Assert.That(spec.LeanSideOffset, Is.EqualTo(MotorSpec.DefaultLeanSideOffset));
            Assert.That(spec.LeanAngleDegrees, Is.EqualTo(MotorSpec.DefaultLeanAngleDegrees));
            Assert.That(spec.LeanSpeed, Is.EqualTo(MotorSpec.DefaultLeanSpeed));
            Assert.That(spec.AirJumps, Is.Zero, "the kit does not jump in the air unless a game turns it on");
            Assert.That(spec.AirJumpSpeed, Is.EqualTo(MotorSpec.DefaultAirJumpSpeed));
        }

        [Test]
        public void LeanAndAirJumpKeepTheGivenValues()
        {
            var spec = new MotorSpec(
                4f, 8f, 1.5f, 9.81f, 0.5f, 2f, leanSideOffset: 0.6f, leanAngleDegrees: 25f, leanSpeed: 3f, airJumps: 2, airJumpSpeed: 5f);

            Assert.That(spec.LeanSideOffset, Is.EqualTo(0.6f));
            Assert.That(spec.LeanAngleDegrees, Is.EqualTo(25f));
            Assert.That(spec.LeanSpeed, Is.EqualTo(3f));
            Assert.That(spec.AirJumps, Is.EqualTo(2));
            Assert.That(spec.AirJumpSpeed, Is.EqualTo(5f));
        }

        [Test]
        public void LeanAndAirJumpRejectBadValuesByName()
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, leanSideOffset: -0.1f)).ParamName,
                Is.EqualTo("leanSideOffset"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, leanAngleDegrees: 91f)).ParamName,
                Is.EqualTo("leanAngleDegrees"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, leanSpeed: 0f)).ParamName,
                Is.EqualTo("leanSpeed"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, airJumps: -1)).ParamName,
                Is.EqualTo("airJumps"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, airJumpSpeed: float.NaN)).ParamName,
                Is.EqualTo("airJumpSpeed"));
        }

        [Test]
        public void DefaultsAreValid()
        {
            Assert.That(MotorSpec.DefaultSafeFallSpeed, Is.GreaterThanOrEqualTo(0f));
            Assert.That(MotorSpec.DefaultLethalFallSpeed, Is.GreaterThan(MotorSpec.DefaultSafeFallSpeed));
            Assert.That(MotorSpec.DefaultDodgeDistance, Is.GreaterThanOrEqualTo(0f));
            Assert.That(MotorSpec.DefaultDodgeSeconds, Is.GreaterThan(0f));
            Assert.That(MotorSpec.DefaultVaultMaxHeight, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void KeepsEveryGivenValue()
        {
            var spec = Spec(10f, 30f, 4f, 0.5f, 1.4f);

            Assert.That(spec.SafeFallSpeed, Is.EqualTo(10f));
            Assert.That(spec.LethalFallSpeed, Is.EqualTo(30f));
            Assert.That(spec.DodgeDistance, Is.EqualTo(4f));
            Assert.That(spec.DodgeSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.VaultMaxHeight, Is.EqualTo(1.4f));
        }

        [Test]
        public void AcceptsZeroForTheNonNegativeValues()
        {
            var spec = Spec(safeFallSpeed: 0f, lethalFallSpeed: 0.1f, dodgeDistance: 0f, vaultMaxHeight: 0f);

            Assert.That(spec.SafeFallSpeed, Is.EqualTo(0f));
            Assert.That(spec.DodgeDistance, Is.EqualTo(0f));
            Assert.That(spec.VaultMaxHeight, Is.EqualTo(0f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteSafeFallSpeed(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(safeFallSpeed: value));
            Assert.That(ex.ParamName, Is.EqualTo("safeFallSpeed"));
        }

        [TestCase(12f)]
        [TestCase(11f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsALethalFallSpeedNotAboveTheSafeOne(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(safeFallSpeed: 12f, lethalFallSpeed: value));
            Assert.That(ex.ParamName, Is.EqualTo("lethalFallSpeed"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteDodgeDistance(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(dodgeDistance: value));
            Assert.That(ex.ParamName, Is.EqualTo("dodgeDistance"));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANonPositiveOrNonFiniteDodgeTime(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(dodgeSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("dodgeSeconds"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteVaultHeight(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Spec(vaultMaxHeight: value));
            Assert.That(ex.ParamName, Is.EqualTo("vaultMaxHeight"));
        }
    }
}
