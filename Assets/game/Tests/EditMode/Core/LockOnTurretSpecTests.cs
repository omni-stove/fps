using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LockOnTurretSpecTests
    {
        [Test]
        public void TheDefaultsTurnBriskAndLookOnlyAboveTheGround()
        {
            var spec = LockOnTurretSpec.Default;

            Assert.That(spec.TurnDegreesPerSecond, Is.EqualTo(120f));
            Assert.That(spec.MinTargetHeight, Is.EqualTo(5f));
            Assert.That(spec.TargetTag, Is.EqualTo(string.Empty));
            Assert.That(spec.AcceptsTag("Untagged"), Is.True, "without a tag anyone is taken");
            Assert.That(spec.AcceptsTag(null), Is.True);
        }

        [Test]
        public void ATargetTagTakesOnlyBodiesWithThatExactTag()
        {
            var spec = new LockOnTurretSpec(90f, 0f, "Player");

            Assert.That(spec.TargetTag, Is.EqualTo("Player"));
            Assert.That(spec.AcceptsTag("Player"), Is.True);
            Assert.That(spec.AcceptsTag("player"), Is.False, "tags compare case by case, as Unity does");
            Assert.That(spec.AcceptsTag("Untagged"), Is.False);
            Assert.That(spec.AcceptsTag(null), Is.False);
            Assert.That(new LockOnTurretSpec(90f, 0f, null).TargetTag, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ABlankTargetTagIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new LockOnTurretSpec(90f, 0f, " "));
            Assert.DoesNotThrow(() => new LockOnTurretSpec(90f, 0f, string.Empty));
        }

        [Test]
        public void OnlyTargetsHighEnoughAboveTheTurretAreTaken()
        {
            var spec = new LockOnTurretSpec(90f, 5f);

            Assert.That(spec.IsHighEnough(0f, 5f), Is.True, "exactly at the height counts");
            Assert.That(spec.IsHighEnough(0f, 4.9f), Is.False);
            Assert.That(spec.IsHighEnough(10f, 14f), Is.False, "the height is measured from the turret, not from zero");
            Assert.That(spec.IsHighEnough(10f, 16f), Is.True);
        }

        [Test]
        public void ANegativeHeightAlsoTakesTargetsBelowTheTurret()
        {
            var spec = new LockOnTurretSpec(90f, -20f);

            Assert.That(spec.IsHighEnough(30f, 15f), Is.True);
            Assert.That(spec.IsHighEnough(30f, 5f), Is.False);
        }

        [Test]
        public void TheTurnSpeedMustBePositiveAndTheHeightFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnTurretSpec(0f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnTurretSpec(-1f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnTurretSpec(float.PositiveInfinity, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnTurretSpec(90f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LockOnTurretSpec(90f, float.NegativeInfinity));
        }
    }
}
