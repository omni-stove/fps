using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class FallDamageCalculatorTests
    {
        // PlayerController・NetworkPlayerMotor の既定値と同じ
        private const float _playerGravity = 20f;
        private const float _playerJumpHeight = 1.2f;

        private static MotorSpec Spec(float safeFallSpeed = 10f, float lethalFallSpeed = 20f)
        {
            return new MotorSpec(5f, 8f, _playerJumpHeight, _playerGravity, 0.1f, 2.5f, safeFallSpeed, lethalFallSpeed);
        }

        [TestCase(0f)]
        [TestCase(5f)]
        [TestCase(10f)]
        public void IsZeroAtOrBelowTheSafeSpeed(float fallSpeed)
        {
            Assert.That(FallDamageCalculator.Calculate(fallSpeed, Spec(), 100), Is.EqualTo(0));
        }

        [TestCase(20f)]
        [TestCase(35f)]
        [TestCase(float.PositiveInfinity)]
        public void IsMaxHealthAtOrAboveTheLethalSpeed(float fallSpeed)
        {
            Assert.That(FallDamageCalculator.Calculate(fallSpeed, Spec(), 100), Is.EqualTo(100));
        }

        [TestCase(12.5f, 25)]
        [TestCase(15f, 50)]
        [TestCase(17.5f, 75)]
        public void IsLinearBetweenTheThresholds(float fallSpeed, int expected)
        {
            Assert.That(FallDamageCalculator.Calculate(fallSpeed, Spec(), 100), Is.EqualTo(expected));
        }

        [Test]
        public void ScalesWithMaxHealth()
        {
            Assert.That(FallDamageCalculator.Calculate(15f, Spec(), 150), Is.EqualTo(75));
        }

        [Test]
        public void RoundsDown()
        {
            // 100 * 0.333.. = 33.3..
            Assert.That(FallDamageCalculator.Calculate(10f + 10f / 3f, Spec(), 100), Is.EqualTo(33));
        }

        [Test]
        public void DoesNotKillFromFullHealthJustBelowTheLethalSpeed()
        {
            var damage = FallDamageCalculator.Calculate(19.999f, Spec(), 100);

            Assert.That(damage, Is.LessThan(100));
            Assert.That(damage, Is.GreaterThan(90));
        }

        [Test]
        public void IsZeroJustAboveTheSafeSpeedForSmallHealth()
        {
            Assert.That(FallDamageCalculator.Calculate(10.001f, Spec(), 1), Is.EqualTo(0));
        }

        [Test]
        public void DefaultThresholdsDoNotHurtANormalJump()
        {
            var spec = new MotorSpec(5f, 8f, _playerJumpHeight, _playerGravity, 0.1f, 2.5f);
            var jumpLanding = (float)Math.Sqrt(2.0 * _playerGravity * _playerJumpHeight);

            Assert.That(FallDamageCalculator.Calculate(jumpLanding, spec, 100), Is.EqualTo(0));
        }

        [Test]
        public void DefaultThresholdsDoNotHurtJumpingOffAVaultableLedge()
        {
            var spec = new MotorSpec(5f, 8f, _playerJumpHeight, _playerGravity, 0.1f, 2.5f);
            var landing = (float)Math.Sqrt(2.0 * _playerGravity * (_playerJumpHeight + spec.VaultMaxHeight));

            Assert.That(FallDamageCalculator.Calculate(landing, spec, 100), Is.EqualTo(0));
        }

        [Test]
        public void DefaultThresholdsHurtAndKillFromHighPlaces()
        {
            var spec = new MotorSpec(5f, 8f, _playerJumpHeight, _playerGravity, 0.1f, 2.5f);

            Assert.That(FallDamageCalculator.Calculate(LandingSpeed(8f), spec, 100), Is.InRange(1, 99));
            Assert.That(FallDamageCalculator.Calculate(LandingSpeed(20f), spec, 100), Is.EqualTo(100));
        }

        [Test]
        public void ADiscreteJumpOnFlatGroundLandsBelowTheDefaultSafeSpeed()
        {
            var spec = new MotorSpec(5f, 8f, _playerJumpHeight, _playerGravity, 0.1f, 2.5f);
            var motor = new PlayerMotor(spec);
            const float dt = 1f / 30f;
            var height = 0f;
            height += motor.Step(System.Numerics.Vector2.Zero, 0f, false, false, true, true, dt).Y;
            var steps = 0;
            while (height > 0f && steps++ < 1000)
            {
                height += motor.Step(System.Numerics.Vector2.Zero, 0f, false, false, false, false, dt).Y;
            }

            Assert.That(-motor.Velocity.Y, Is.LessThan(MotorSpec.DefaultSafeFallSpeed));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidFallSpeed(float fallSpeed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FallDamageCalculator.Calculate(fallSpeed, Spec(), 100));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveMaxHealth(int maxHealth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FallDamageCalculator.Calculate(15f, Spec(), maxHealth));
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => FallDamageCalculator.Calculate(15f, null, 100));
        }

        private static float LandingSpeed(float dropHeight)
        {
            return (float)Math.Sqrt(2.0 * _playerGravity * dropHeight);
        }
    }
}
