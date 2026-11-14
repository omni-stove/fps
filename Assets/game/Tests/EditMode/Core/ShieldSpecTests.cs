using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ShieldSpecTests
    {
        private static MeleeSpec Bash(float reach = 1.8f) => new MeleeSpec(30, reach, 90f, 0.6f, 1f, 0.3f, 0.5f);

        [Test]
        public void TheDefaultsCanReachFromWhereTheyStop()
        {
            var spec = ShieldSpec.Default;

            Assert.That(spec.HoldDistance, Is.LessThanOrEqualTo(spec.Bash.Reach));
            Assert.That(spec.ApproachSpeedMultiplier, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(spec.Bash, Is.SameAs(ShieldSpec.DefaultBash));
        }

        [Test]
        public void OnlyMeleeAndExplosionsLowerTheShield()
        {
            var spec = new ShieldSpec(0.5f, 1f, 1.5f, 2.5f, Bash());

            Assert.That(spec.LowerSecondsFor(DamageKind.Melee), Is.EqualTo(1.5f));
            Assert.That(spec.LowerSecondsFor(DamageKind.Explosion), Is.EqualTo(2.5f));
            Assert.That(spec.LowerSecondsFor(DamageKind.Bullet), Is.EqualTo(0f));
            Assert.That(spec.LowerSecondsFor(DamageKind.Other), Is.EqualTo(0f));
            Assert.That(spec.LowerSecondsFor(DamageKind.Environment), Is.EqualTo(0f));
        }

        [Test]
        public void StoppingOutOfReachIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(0.5f, 2f, 1f, 1f, Bash(1.8f)));
            Assert.DoesNotThrow(() => _ = new ShieldSpec(0.5f, 1.8f, 1f, 1f, Bash(1.8f)));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ShieldSpec(0.5f, 1f, 1f, 1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(0f, 1f, 1f, 1f, Bash()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(1.5f, 1f, 1f, 1f, Bash()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(float.NaN, 1f, 1f, 1f, Bash()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(0.5f, 0f, 1f, 1f, Bash()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(0.5f, 1f, -1f, 1f, Bash()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ShieldSpec(0.5f, 1f, 1f, float.PositiveInfinity, Bash()));
        }

        [Test]
        public void EnemySpecCarriesTheShieldAndSniperValues()
        {
            var defaults = new EnemySpec(20f, 10f, 1f, 2f, 0.5f);
            Assert.That(defaults.Shield, Is.SameAs(ShieldSpec.Default));
            Assert.That(defaults.Sniper, Is.SameAs(SniperSpec.Default));

            var shield = new ShieldSpec(0.4f, 1f, 2f, 3f, Bash());
            var sniper = new SniperSpec(3f, 2f, 0.5f, 6f);
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Shield, shield: shield, sniper: sniper);

            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Shield));
            Assert.That(spec.Shield, Is.SameAs(shield));
            Assert.That(spec.Sniper, Is.SameAs(sniper));
        }

        [Test]
        public void WaveStrengthKeepsTheArchetypeValues()
        {
            var shield = new ShieldSpec(0.4f, 1f, 2f, 3f, Bash());
            var sniper = new SniperSpec(3f, 2f, 0.5f, 6f);
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Sniper, shield: shield, sniper: sniper);

            var stronger = WaveStrength.Apply(spec, 2f);

            Assert.That(stronger.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(stronger.Shield, Is.SameAs(shield));
            Assert.That(stronger.Sniper, Is.SameAs(sniper));
        }
    }
}
