using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ImpactResolverTests
    {
        private static readonly DamageFalloff _falloff = new DamageFalloff(10f, 50f, 0.5f);

        [TestCase(HitZone.Head, 0f)]
        [TestCase(HitZone.Body, 30f)]
        [TestCase(HitZone.Limb, 80f)]
        public void CharacterDamageMatchesTheDamageCalculator(HitZone zone, float distance)
        {
            var impact = new ImpactSpec(33, _falloff);

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Character(zone, Vector3.One, distance, ZoneDamageMultipliers.Default));

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
        public void AreasAddNothingForNow()
        {
            var impact = new ImpactSpec(25, areaEffect: new AreaEffectSpec(4f, 5f, 1f, 2));

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f, ZoneDamageMultipliers.Default));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Damage));
        }

        [Test]
        public void ACharacterHitAfflictsTheStatusEffectAfterTheDamage()
        {
            var burn = new StatusEffectSpec(3f, 0.5f, 4);
            var point = new Vector3(1f, 2f, 3f);

            var orders = ImpactResolver.Resolve(new ImpactSpec(25, statusEffect: burn), ImpactHit.Character(HitZone.Body, point, 0f, ZoneDamageMultipliers.Default));

            Assert.That(orders.Count, Is.EqualTo(2));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Damage));
            Assert.That(orders[1].Kind, Is.EqualTo(ImpactOrderKind.StatusEffect));
            Assert.That(orders[1].StatusEffect, Is.SameAs(burn));
            Assert.That(orders[1].Point, Is.EqualTo(point));
        }

        [Test]
        public void AStructureHitAfflictsNothing()
        {
            var impact = new ImpactSpec(25, statusEffect: new StatusEffectSpec(3f, 0.5f, 4));

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Structure(Vector3.Zero, 0f));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.StructureDamage));
        }

        [Test]
        public void AnImpactExplosionDetonatesWhereTheRoundHitACharacterAfterTheDamage()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(100, 5f));
            var point = new Vector3(1f, 2f, 3f);

            var orders = ImpactResolver.Resolve(new ImpactSpec(25, explosion: explosion), ImpactHit.Character(HitZone.Body, point, 7f, ZoneDamageMultipliers.Default));

            Assert.That(orders.Count, Is.EqualTo(2));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Damage));
            Assert.That(orders[1].Kind, Is.EqualTo(ImpactOrderKind.Explosion));
            Assert.That(orders[1].Explosion, Is.SameAs(explosion));
            Assert.That(orders[1].Point, Is.EqualTo(point));
        }

        [Test]
        public void AnImpactExplosionDetonatesWhereTheRoundHitAStructure()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(100, 5f), 80);
            var point = new Vector3(-4f, 0.5f, 9f);

            var orders = ImpactResolver.Resolve(new ImpactSpec(25, structureRadius: 2f, explosion: explosion), ImpactHit.Structure(point, 12f));

            Assert.That(orders.Count, Is.EqualTo(2));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.StructureDamage));
            Assert.That(orders[1].Kind, Is.EqualTo(ImpactOrderKind.Explosion));
            Assert.That(orders[1].Explosion, Is.SameAs(explosion));
            Assert.That(orders[1].Point, Is.EqualTo(point));
        }

        [TestCase(DetonationTrigger.OnStop)]
        [TestCase(DetonationTrigger.OnLifetimeEnd)]
        public void AnExplosionThatWaitsForTheRoundToEndDoesNotDetonateOnTheHit(DetonationTrigger detonation)
        {
            var impact = new ImpactSpec(25, explosion: new ExplosionSpec(new ExplosionDamage(100, 5f), 0, detonation));

            var orders = ImpactResolver.Resolve(impact, ImpactHit.Structure(Vector3.Zero, 3f));

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.StructureDamage));
        }

        [TestCase(DetonationTrigger.OnImpact, DetonationTrigger.OnImpact, true)]
        [TestCase(DetonationTrigger.OnStop, DetonationTrigger.OnStop, true)]
        [TestCase(DetonationTrigger.OnLifetimeEnd, DetonationTrigger.OnLifetimeEnd, true)]
        [TestCase(DetonationTrigger.OnStop, DetonationTrigger.OnLifetimeEnd, false)]
        [TestCase(DetonationTrigger.OnLifetimeEnd, DetonationTrigger.OnStop, false)]
        [TestCase(DetonationTrigger.OnImpact, DetonationTrigger.OnStop, false)]
        public void ARoundEndingDetonatesOnlyTheMatchingExplosion(DetonationTrigger detonation, DetonationTrigger cause, bool detonates)
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(100, 5f), 0, detonation);
            var point = new Vector3(3f, 1f, -2f);

            var orders = ImpactResolver.ResolveDetonation(new ImpactSpec(25, explosion: explosion), cause, point);

            Assert.That(orders.Count, Is.EqualTo(detonates ? 1 : 0));
            if (detonates)
            {
                Assert.That(orders[0].Kind, Is.EqualTo(ImpactOrderKind.Explosion));
                Assert.That(orders[0].Explosion, Is.SameAs(explosion));
                Assert.That(orders[0].Point, Is.EqualTo(point));
            }
        }

        [Test]
        public void ARoundWithoutAnExplosionDetonatesNothingWhenItEnds()
        {
            Assert.That(ImpactResolver.ResolveDetonation(new ImpactSpec(25), DetonationTrigger.OnStop, Vector3.Zero), Is.Empty);
        }

        [Test]
        public void TheDetonationPointStandsOffTheSurfaceAlongTheNormal()
        {
            var point = new Vector3(2f, 0.5f, 1f);

            var center = ImpactResolver.DetonationPoint(point, new Vector3(0f, 4f, 0f));

            Assert.That(center.X, Is.EqualTo(point.X));
            Assert.That(center.Y, Is.EqualTo(point.Y + ImpactResolver.DetonationStandoff).Within(1e-6f));
            Assert.That(center.Z, Is.EqualTo(point.Z));
            Assert.That(ImpactResolver.DetonationPoint(point, Vector3.Zero), Is.EqualTo(point));
            Assert.That(ImpactResolver.DetonationPoint(point, new Vector3(float.NaN, 0f, 0f)), Is.EqualTo(point));
        }

        [Test]
        public void TheChargeScalesTheDamage()
        {
            var charge = new ChargeSpec(1f, 0.2f, 3f);
            var impact = new ImpactSpec(30);
            var hit = ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f, ZoneDamageMultipliers.Default);

            Assert.That(ImpactResolver.Resolve(impact, hit, charge, 1f)[0].Amount, Is.EqualTo(90));
            Assert.That(ImpactResolver.Resolve(impact, hit, charge, 0.5f)[0].Amount, Is.EqualTo(60));
            Assert.That(ImpactResolver.Resolve(impact, hit, null, 1f)[0].Amount, Is.EqualTo(30));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => ImpactResolver.Resolve(null, ImpactHit.Structure(Vector3.Zero, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactHit.Structure(Vector3.Zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactHit.Character(HitZone.Body, Vector3.Zero, float.NaN, ZoneDamageMultipliers.Default));
            Assert.Throws<ArgumentNullException>(() => ImpactHit.Character(HitZone.Body, Vector3.Zero, 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactOrder.Damage(-1));
            Assert.Throws<ArgumentNullException>(() => ImpactOrder.Detonate(null, Vector3.Zero));
            Assert.Throws<ArgumentNullException>(() => ImpactResolver.ResolveDetonation(null, DetonationTrigger.OnStop, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactResolver.ResolveDetonation(ImpactSpec.Default, (DetonationTrigger)99, Vector3.Zero));
        }
    }
}
