using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ChargerBomberSpecTests
    {
        [Test]
        public void TheChargerDefaultsAreValid()
        {
            var spec = ChargerSpec.Default;

            Assert.That(spec.TriggerDistance, Is.EqualTo(ChargerSpec.DefaultTriggerDistance));
            Assert.That(spec.WindupSeconds, Is.EqualTo(ChargerSpec.DefaultWindupSeconds));
            Assert.That(spec.ImpactDamage, Is.EqualTo(ChargerSpec.DefaultImpactDamage));
            Assert.That(spec.ChargeSeconds, Is.EqualTo(ChargerSpec.DefaultChargeDistance / ChargerSpec.DefaultChargeSpeed));
            Assert.That(spec.TriggerDistance, Is.LessThanOrEqualTo(spec.ChargeDistance));
        }

        [Test]
        public void TheChargerRejectsValuesThatBreakTheTell()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 0f, 10f, 10f, 1f, 30, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(12f, 1f, 10f, 10f, 1f, 30, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(0f, 1f, 10f, 10f, 1f, 30, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 1f, 0f, 10f, 1f, 30, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 1f, 10f, 10f, 0f, 30, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 1f, 10f, 10f, 1f, 0, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 1f, 10f, 10f, 1f, 30, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ChargerSpec(7f, 1f, 10f, 10f, 1f, 30, 1f, float.NaN));
        }

        [Test]
        public void TheBomberDefaultsAreValid()
        {
            var spec = BomberSpec.Default;

            Assert.That(spec.FuseSeconds, Is.EqualTo(BomberSpec.DefaultFuseSeconds));
            Assert.That(spec.Blast.MaxDamage, Is.EqualTo(BomberSpec.DefaultBlastDamage));
            Assert.That(spec.DeathBlast.Radius, Is.EqualTo(BomberSpec.DefaultDeathBlastRadius));
            Assert.That(spec.DeathBlast.MaxDamage, Is.LessThan(spec.Blast.MaxDamage));
            Assert.That(spec.TriggerDistance, Is.LessThan(spec.Blast.Radius));
        }

        [Test]
        public void TheBomberRejectsValuesThatBreakTheSignal()
        {
            var blast = new ExplosionDamage(80, 4f, 1f);
            var small = new ExplosionDamage(20, 2f);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BomberSpec(3f, 0f, 1f, blast, small));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BomberSpec(4f, 1f, 1f, blast, small));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BomberSpec(3f, 1f, 0f, blast, small));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BomberSpec(3f, 1f, 1f, blast, new ExplosionDamage(90, 2f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BomberSpec(3f, 1f, 1f, blast, new ExplosionDamage(20, 5f)));
            Assert.Throws<ArgumentNullException>(() => _ = new BomberSpec(3f, 1f, 1f, null, small));
            Assert.Throws<ArgumentNullException>(() => _ = new BomberSpec(3f, 1f, 1f, blast, null));
        }

        [Test]
        public void TheEnemySpecKeepsTheChargerAndBomberValues()
        {
            var charger = new ChargerSpec(5f, 0.5f, 9f, 9f, 1f, 20, 1f, 1f);
            var bomber = new BomberSpec(2f, 1f, 1.2f, new ExplosionDamage(50, 3f), new ExplosionDamage(10, 1f));

            var defaults = new EnemySpec(20f, 10f, 1f, 2f, 0.5f);
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Charger, charger: charger, bomber: bomber);

            Assert.That(defaults.Charger, Is.SameAs(ChargerSpec.Default));
            Assert.That(defaults.Bomber, Is.SameAs(BomberSpec.Default));
            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Charger));
            Assert.That(spec.Charger, Is.SameAs(charger));
            Assert.That(spec.Bomber, Is.SameAs(bomber));
        }

        [Test]
        public void AStrongerWaveKeepsTheChargerAndBomberValues()
        {
            var charger = new ChargerSpec(5f, 0.5f, 9f, 9f, 1f, 20, 1f, 1f);
            var bomber = new BomberSpec(2f, 1f, 1.2f, new ExplosionDamage(50, 3f), new ExplosionDamage(10, 1f));
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Bomber, charger: charger, bomber: bomber);

            var stronger = WaveStrength.Apply(spec, 2f);

            Assert.That(stronger.Archetype, Is.EqualTo(EnemyArchetype.Bomber));
            Assert.That(stronger.Charger, Is.SameAs(charger));
            Assert.That(stronger.Bomber, Is.SameAs(bomber));
        }
    }
}
