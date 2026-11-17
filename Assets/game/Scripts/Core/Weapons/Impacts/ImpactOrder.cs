using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct ImpactOrder
    {
        private ImpactOrder(
            ImpactOrderKind kind,
            int amount,
            float radius,
            Vector3 point,
            ExplosionSpec explosion,
            StatusEffectSpec statusEffect,
            AreaEffectSpec areaEffect)
        {
            Kind = kind;
            Amount = amount;
            Radius = radius;
            Point = point;
            Explosion = explosion;
            StatusEffect = statusEffect;
            AreaEffect = areaEffect;
        }

        public ImpactOrderKind Kind { get; }

        public int Amount { get; }

        public float Radius { get; }

        public Vector3 Point { get; }

        public ExplosionSpec Explosion { get; }

        public StatusEffectSpec StatusEffect { get; }

        public AreaEffectSpec AreaEffect { get; }

        public static ImpactOrder Damage(int amount)
        {
            return new ImpactOrder(ImpactOrderKind.Damage, SpecGuard.NonNegative(amount, nameof(amount)), 0f, Vector3.Zero, null, null, null);
        }

        public static ImpactOrder StructureDamage(int amount, float radius, Vector3 point)
        {
            return new ImpactOrder(
                ImpactOrderKind.StructureDamage,
                SpecGuard.NonNegative(amount, nameof(amount)),
                SpecGuard.NonNegativeFinite(radius, nameof(radius)),
                point,
                null,
                null,
                null);
        }

        public static ImpactOrder Detonate(ExplosionSpec explosion, Vector3 point)
        {
            return new ImpactOrder(
                ImpactOrderKind.Explosion, 0, 0f, point, explosion ?? throw new ArgumentNullException(nameof(explosion)), null, null);
        }

        public static ImpactOrder Afflict(StatusEffectSpec statusEffect, Vector3 point)
        {
            return new ImpactOrder(
                ImpactOrderKind.StatusEffect, 0, 0f, point, null, statusEffect ?? throw new ArgumentNullException(nameof(statusEffect)), null);
        }

        public static ImpactOrder Spread(AreaEffectSpec areaEffect, Vector3 point)
        {
            return new ImpactOrder(
                ImpactOrderKind.AreaEffect, 0, 0f, point, null, null, areaEffect ?? throw new ArgumentNullException(nameof(areaEffect)));
        }
    }
}
