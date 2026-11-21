using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HorseSpecTests
    {
        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = HorseSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(500f));
            Assert.That(spec.WalkSpeed, Is.EqualTo(1.8f));
            Assert.That(spec.TrotSpeed, Is.EqualTo(4.5f));
            Assert.That(spec.CanterSpeed, Is.EqualTo(9f));
            Assert.That(spec.ReverseSpeed, Is.EqualTo(1.2f));
            Assert.That(spec.Acceleration, Is.EqualTo(4f));
            Assert.That(spec.Deceleration, Is.EqualTo(8f));
            Assert.That(spec.TurnRate, Is.EqualTo(120f));
            Assert.That(spec.CanterTurnScale, Is.EqualTo(0.35f));
            Assert.That(spec.JumpSpeed, Is.EqualTo(5f));
            Assert.That(spec.MaxSlopeAngle, Is.EqualTo(40f));
            Assert.That(spec.WalkThreshold, Is.EqualTo(0.2f));
            Assert.That(spec.TrotThreshold, Is.EqualTo(2.5f));
            Assert.That(spec.CanterThreshold, Is.EqualTo(6f));
        }

        [Test]
        public void TheSpecKeepsTheGivenValues()
        {
            var spec = new HorseSpec(
                mass: 450f,
                walkSpeed: 2f,
                trotSpeed: 5f,
                canterSpeed: 10f,
                reverseSpeed: 1f,
                acceleration: 3f,
                deceleration: 6f,
                turnRate: 90f,
                canterTurnScale: 0.5f,
                jumpSpeed: 4f,
                maxSlopeAngle: 35f,
                walkThreshold: 0.3f,
                trotThreshold: 3f,
                canterThreshold: 7f);

            Assert.That(spec.Mass, Is.EqualTo(450f));
            Assert.That(spec.WalkSpeed, Is.EqualTo(2f));
            Assert.That(spec.TrotSpeed, Is.EqualTo(5f));
            Assert.That(spec.CanterSpeed, Is.EqualTo(10f));
            Assert.That(spec.ReverseSpeed, Is.EqualTo(1f));
            Assert.That(spec.Acceleration, Is.EqualTo(3f));
            Assert.That(spec.Deceleration, Is.EqualTo(6f));
            Assert.That(spec.TurnRate, Is.EqualTo(90f));
            Assert.That(spec.CanterTurnScale, Is.EqualTo(0.5f));
            Assert.That(spec.JumpSpeed, Is.EqualTo(4f));
            Assert.That(spec.MaxSlopeAngle, Is.EqualTo(35f));
            Assert.That(spec.WalkThreshold, Is.EqualTo(0.3f));
            Assert.That(spec.TrotThreshold, Is.EqualTo(3f));
            Assert.That(spec.CanterThreshold, Is.EqualTo(7f));
        }

        [Test]
        public void TheSpecAcceptsTheEdgesOfItsRanges()
        {
            Assert.DoesNotThrow(() => new HorseSpec(walkSpeed: 3f, trotSpeed: 3f, canterSpeed: 9f, walkThreshold: 0.2f, trotThreshold: 2.5f));
            Assert.DoesNotThrow(() => new HorseSpec(trotSpeed: 9f, canterSpeed: 9f));
            Assert.DoesNotThrow(() => new HorseSpec(reverseSpeed: 0f));
            Assert.DoesNotThrow(() => new HorseSpec(reverseSpeed: 1.8f));
            Assert.DoesNotThrow(() => new HorseSpec(turnRate: 0f));
            Assert.DoesNotThrow(() => new HorseSpec(canterTurnScale: 0f));
            Assert.DoesNotThrow(() => new HorseSpec(canterTurnScale: 1f));
            Assert.DoesNotThrow(() => new HorseSpec(jumpSpeed: 0f));
            Assert.DoesNotThrow(() => new HorseSpec(walkThreshold: 1.8f));
            Assert.DoesNotThrow(() => new HorseSpec(trotThreshold: 4.5f));
            Assert.DoesNotThrow(() => new HorseSpec(canterThreshold: 9f));
        }

        [Test]
        public void TheSpecRejectsANonPositiveMass()
        {
            var zero = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(mass: 0f));
            Assert.That(zero.ParamName, Is.EqualTo("mass"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(mass: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(mass: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(mass: float.PositiveInfinity));
        }

        [Test]
        public void TheSpecRejectsNegativeSpeeds()
        {
            var walk = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(walkSpeed: -1f));
            Assert.That(walk.ParamName, Is.EqualTo("walkSpeed"));
            var trot = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(trotSpeed: -1f));
            Assert.That(trot.ParamName, Is.EqualTo("trotSpeed"));
            var canter = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterSpeed: -1f));
            Assert.That(canter.ParamName, Is.EqualTo("canterSpeed"));
            var reverse = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(reverseSpeed: -1f));
            Assert.That(reverse.ParamName, Is.EqualTo("reverseSpeed"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(walkSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterSpeed: float.NaN));
        }

        [Test]
        public void TheSpecRejectsGaitSpeedsOutOfOrder()
        {
            var trot = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(trotSpeed: 1f));
            Assert.That(trot.ParamName, Is.EqualTo("trotSpeed"));
            var canter = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterSpeed: 4f));
            Assert.That(canter.ParamName, Is.EqualTo("canterSpeed"));
            var reverse = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(reverseSpeed: 2f));
            Assert.That(reverse.ParamName, Is.EqualTo("reverseSpeed"));
        }

        [Test]
        public void TheSpecRejectsMotionValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(acceleration: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(deceleration: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(turnRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterTurnScale: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterTurnScale: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterTurnScale: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(jumpSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(maxSlopeAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(maxSlopeAngle: 90f));
        }

        [Test]
        public void TheSpecRejectsGaitThresholdsOutOfOrder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(walkThreshold: 0f));
            var trot = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(trotThreshold: 0.2f));
            Assert.That(trot.ParamName, Is.EqualTo("trotThreshold"));
            var canter = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterThreshold: 2.5f));
            Assert.That(canter.ParamName, Is.EqualTo("canterThreshold"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterThreshold: float.PositiveInfinity));
        }

        [Test]
        public void TheSpecRejectsGaitThresholdsAboveTheirTopSpeeds()
        {
            var walk = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(walkThreshold: 2f));
            Assert.That(walk.ParamName, Is.EqualTo("walkThreshold"));
            var trot = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(trotThreshold: 5f));
            Assert.That(trot.ParamName, Is.EqualTo("trotThreshold"));
            var canter = Assert.Throws<ArgumentOutOfRangeException>(() => new HorseSpec(canterThreshold: 10f));
            Assert.That(canter.ParamName, Is.EqualTo("canterThreshold"));
        }
    }
}
