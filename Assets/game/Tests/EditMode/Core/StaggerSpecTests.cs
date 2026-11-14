using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaggerSpecTests
    {
        [Test]
        public void EnemiesStaggerFromEveryKindAndThePlayerOnlyFromMeleeAndExplosions()
        {
            var enemy = StaggerSpec.EnemyDefault;
            Assert.That(enemy.SecondsFor(StaggerKind.Flinch), Is.GreaterThan(0f));
            Assert.That(enemy.SecondsFor(StaggerKind.Kneel), Is.GreaterThan(0f));
            Assert.That(enemy.SecondsFor(StaggerKind.Stumble), Is.GreaterThan(0f));
            Assert.That(enemy.SecondsFor(StaggerKind.Blast), Is.GreaterThan(0f));
            Assert.That(enemy.MoveMultiplier, Is.EqualTo(1f));

            var player = StaggerSpec.PlayerDefault;
            Assert.That(player.SecondsFor(StaggerKind.Flinch), Is.Zero);
            Assert.That(player.SecondsFor(StaggerKind.Kneel), Is.Zero);
            Assert.That(player.SecondsFor(StaggerKind.Stumble), Is.GreaterThan(0f));
            Assert.That(player.SecondsFor(StaggerKind.Blast), Is.GreaterThan(0f));
            Assert.That(player.MoveMultiplier, Is.LessThan(1f));
            Assert.That(player.TurnMultiplier, Is.LessThan(1f));
        }

        [Test]
        public void ARifleLegShotIsEnoughToKneelAtAnyRange()
        {
            var farLegShot = DamageCalculator.Calculate(25, HitZone.Limb, 1000f, new DamageFalloff(20f, 60f, 0.5f));

            Assert.That(farLegShot, Is.GreaterThanOrEqualTo(StaggerSpec.DefaultEnemyLimbDamage));
        }

        [Test]
        public void MapsEachKindToItsValues()
        {
            var spec = new StaggerSpec(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 0.5f, 0.25f);

            Assert.That(spec.SecondsFor(StaggerKind.None), Is.Zero);
            Assert.That(spec.SecondsFor(StaggerKind.Flinch), Is.EqualTo(1f));
            Assert.That(spec.SecondsFor(StaggerKind.Kneel), Is.EqualTo(2f));
            Assert.That(spec.SecondsFor(StaggerKind.Stumble), Is.EqualTo(3f));
            Assert.That(spec.SecondsFor(StaggerKind.Blast), Is.EqualTo(4f));
            Assert.That(spec.DamageFor(StaggerKind.None), Is.Zero);
            Assert.That(spec.DamageFor(StaggerKind.Flinch), Is.EqualTo(5f));
            Assert.That(spec.DamageFor(StaggerKind.Kneel), Is.EqualTo(6f));
            Assert.That(spec.DamageFor(StaggerKind.Stumble), Is.EqualTo(7f));
            Assert.That(spec.DamageFor(StaggerKind.Blast), Is.EqualTo(8f));
            Assert.That(spec.WindowSeconds, Is.EqualTo(9f));
            Assert.That(spec.ImmunitySeconds, Is.EqualTo(10f));
            Assert.That(spec.MoveMultiplier, Is.EqualTo(0.5f));
            Assert.That(spec.TurnMultiplier, Is.EqualTo(0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.SecondsFor((StaggerKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.DamageFor((StaggerKind)99));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(float.PositiveInfinity, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(0f, 0f, 0f, 0f, float.NaN, 0f, 0f, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StaggerSpec(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, -0.1f));
        }

        [Test]
        public void EnemySpecKeepsItsStaggerResistance()
        {
            Assert.That(new EnemySpec(20f, 10f, 1f, 2f, 0.5f).StaggerResistance, Is.Zero);
            Assert.That(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, staggerResistance: 1.5f).StaggerResistance, Is.EqualTo(1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, staggerResistance: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, staggerResistance: float.PositiveInfinity));
        }

        [Test]
        public void WaveStrengthKeepsTheStaggerResistance()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, staggerResistance: 2f);

            Assert.That(WaveStrength.Apply(spec, 1.5f).StaggerResistance, Is.EqualTo(2f));
        }
    }
}
