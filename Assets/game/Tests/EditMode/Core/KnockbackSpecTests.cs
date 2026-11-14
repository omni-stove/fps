using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KnockbackSpecTests
    {
        private static KnockbackSpec Spec(float speed = 10f, float upward = 0.5f, float weightFactor = 1f, float minSpeed = 1f)
        {
            var force = new KnockbackForce(speed, upward, weightFactor);
            return new KnockbackSpec(explosion: force, melee: force, charge: force, minSpeed: minSpeed);
        }

        [Test]
        public void PushesAwayAndTiltsUpByTheUpwardRatio()
        {
            Assert.That(Spec().TryGetVelocity(DamageKind.Explosion, new Vector3(3f, -1f, 0f), 1f, 1f, out var velocity), Is.True);

            Assert.That(velocity.Length(), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(velocity.X, Is.GreaterThan(0f));
            Assert.That(velocity.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(velocity.Y, Is.EqualTo(velocity.X).Within(1e-4f), "half up means 45 degrees");
        }

        [Test]
        public void ZeroUpwardRatioPushesFlat()
        {
            Spec(upward: 0f).TryGetVelocity(DamageKind.Melee, new Vector3(0f, 2f, -1f), 1f, 1f, out var velocity);

            Assert.That(velocity.Y, Is.Zero);
            Assert.That(velocity.Z, Is.EqualTo(-10f).Within(1e-4f));
        }

        [Test]
        public void StrengthAndWeightScaleTheSpeed()
        {
            var spec = Spec(weightFactor: 1f);

            spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 0.5f, 1f, out var half);
            spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 1f, 2f, out var heavy);

            Assert.That(half.Length(), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(heavy.Length(), Is.EqualTo(5f).Within(1e-4f), "weight factor 1 divides by the weight");
        }

        [Test]
        public void ZeroWeightFactorIgnoresTheWeight()
        {
            Spec(weightFactor: 0f).TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 1f, 4f, out var velocity);

            Assert.That(velocity.Length(), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void TooWeakAPushDoesNotLaunch()
        {
            var spec = Spec(minSpeed: 3f);

            Assert.That(spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 0.2f, 1f, out var velocity), Is.False);
            Assert.That(velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 0f, 1f, out _), Is.False);
        }

        [Test]
        public void BulletsAndOtherDamageDoNotLaunch()
        {
            var spec = Spec();

            Assert.That(spec.For(DamageKind.Bullet).Speed, Is.Zero);
            Assert.That(spec.For(DamageKind.Other).Speed, Is.Zero);
            Assert.That(spec.TryGetVelocity(DamageKind.Bullet, Vector3.UnitX, 1f, 1f, out _), Is.False);
            Assert.That(spec.TryGetVelocity(DamageKind.Other, Vector3.UnitX, 1f, 1f, out _), Is.False);
        }

        [Test]
        public void AStraightUpPushLaunchesUpOnlyWhenThereIsAnUpwardRatio()
        {
            Assert.That(Spec(upward: 0.3f).TryGetVelocity(DamageKind.Explosion, Vector3.UnitY, 1f, 1f, out var velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(new Vector3(0f, 10f, 0f)));

            Assert.That(Spec(upward: 0f).TryGetVelocity(DamageKind.Explosion, -Vector3.UnitY, 1f, 1f, out _), Is.False);
        }

        [Test]
        public void OnlyMeleeAndChargeComeFromTheReport()
        {
            Assert.That(KnockbackSpec.ComesFromReport(DamageKind.Melee), Is.True);
            Assert.That(KnockbackSpec.ComesFromReport(DamageKind.Charge), Is.True);
            Assert.That(KnockbackSpec.ComesFromReport(DamageKind.Explosion), Is.False, "the explosion adds its own strength");
            Assert.That(KnockbackSpec.ComesFromReport(DamageKind.Bullet), Is.False);
            Assert.That(KnockbackSpec.ComesFromReport(DamageKind.Other), Is.False);
        }

        [Test]
        public void DefaultsLaunchEveryForcefulKind()
        {
            var spec = KnockbackSpec.Default;

            Assert.That(spec.Explosion.Speed, Is.EqualTo(KnockbackSpec.DefaultExplosionSpeed));
            Assert.That(spec.Melee.Speed, Is.EqualTo(KnockbackSpec.DefaultMeleeSpeed));
            Assert.That(spec.Charge.Speed, Is.EqualTo(KnockbackSpec.DefaultChargeSpeed));
            Assert.That(spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 1f, KnockbackSpec.DefaultWeight, out _), Is.True);
            Assert.That(spec.TryGetVelocity(DamageKind.Melee, Vector3.UnitX, 1f, KnockbackSpec.DefaultWeight, out _), Is.True);
            Assert.That(spec.TryGetVelocity(DamageKind.Charge, Vector3.UnitX, 1f, KnockbackSpec.DefaultWeight, out _), Is.True);
        }

        [Test]
        public void RejectsInvalidForces()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackForce(-1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackForce(float.PositiveInfinity, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackForce(1f, 1.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackForce(1f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackForce(1f, 0f, -0.1f));
        }

        [Test]
        public void RejectsInvalidSpecs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(minSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(maxAirSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(minAirSeconds: 2f, maxAirSeconds: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(gravity: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(recoverSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(landingSnapRadius: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnockbackSpec(airControl: float.PositiveInfinity));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var spec = Spec();

            Assert.Throws<ArgumentOutOfRangeException>(() => spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 1.5f, 1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, -0.1f, 1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.TryGetVelocity(DamageKind.Explosion, Vector3.UnitX, 1f, 0f, out _));
            Assert.Throws<ArgumentException>(() => spec.TryGetVelocity(DamageKind.Explosion, new Vector3(float.NaN, 0f, 0f), 1f, 1f, out _));
        }
    }
}
