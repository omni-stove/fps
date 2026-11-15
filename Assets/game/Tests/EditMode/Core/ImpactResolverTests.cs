using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ImpactResolverTests
    {
        private static readonly DamageFalloff _falloff = new DamageFalloff(10f, 50f, 0.5f);

        [TestCase(HitZone.Head, 0f)]
        [TestCase(HitZone.Body, 30f)]
        [TestCase(HitZone.Limb, 80f)]
        public void CharacterDamageMatchesTheDamageCalculator(HitZone zone, float distance)
        {
            var impact = new ImpactSpec(33, _falloff);

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Character(zone, Vector3.One, distance));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Damage));
            Assert.That(orders[0].Amount, Is.EqualTo(DamageCalculator.Calculate(33, zone, distance, _falloff)));
        }

        [Test]
        public void CharacterDamageUsesTheVictimsZoneMultipliers()
        {
            var multipliers = new ZoneDamageMultipliers(3.0, 1.0, 0.5, 2.0);
            var impact = new ImpactSpec(20, _falloff);

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Character(HitZone.Head, Vector3.Zero, 20f, multipliers));

            Assert.That(orders[0].Amount, Is.EqualTo(DamageCalculator.Calculate(20, HitZone.Head, 20f, _falloff, multipliers)));
        }

        [Test]
        public void StructuresTakeTheUnscaledDamageOverTheStructureRadius()
        {
            var point = new Vector3(4f, 5f, 6f);
            var impact = new ImpactSpec(40, _falloff, structureRadius: 2.5f);

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Structure(point, 90f));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.StructureDamage));
            Assert.That(orders[0].Amount, Is.EqualTo(40));
            Assert.That(orders[0].Radius, Is.EqualTo(2.5f));
            Assert.That(orders[0].Point, Is.EqualTo(point));
        }

        [Test]
        public void ExplosionsStatusEffectsAndAreasAddNothingForNow()
        {
            var impact = new ImpactSpec(
                25,
                explosion: new ExplosionSpec(new ExplosionDamage(100, 5f)),
                statusEffect: new StatusEffectSpec(3f, 0.5f, 4),
                areaEffect: new AreaEffectSpec(4f, 5f, 1f, 2));

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Damage));
        }

        [Test]
        public void ChargeDoesNotScaleTheDamageYet()
        {
            var charge = new ChargeSpec(1f, 0.2f, 3f);
            var impact = new ImpactSpec(30);

            Assert.That(ChargeScaling.DamageMultiplier(charge, 1f), Is.EqualTo(1f));
            Assert.That(ChargeScaling.SpeedMultiplier(charge, 1f), Is.EqualTo(1f));
            Assert.That(
                ImpactResolver.Resolve(impact, ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f), charge, 1f)[0].Amount,
                Is.EqualTo(30));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => ImpactResolver.Resolve(null, ImpactHit.Structure(Vector3.Zero, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactHit.Structure(Vector3.Zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactHit.Character(HitZone.Body, Vector3.Zero, float.NaN));
            Assert.Throws<ArgumentNullException>(() => ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactOrder.Damage(-1));
            Assert.Throws<ArgumentNullException>(() => ImpactOrder.Detonate(null, Vector3.Zero));
        }
    }
}
