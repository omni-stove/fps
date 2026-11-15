namespace Fps.Core
{
    public sealed class ImpactSpec
    {
        public const int DefaultDamage = 25;

        public static readonly ImpactSpec Default = new ImpactSpec();

        public ImpactSpec(
            int damage = DefaultDamage,
            DamageFalloff falloff = null,
            float structureRadius = 0f,
            ExplosionSpec explosion = null,
            StatusEffectSpec statusEffect = null,
            AreaEffectSpec areaEffect = null)
        {
            Damage = SpecGuard.NonNegative(damage, nameof(damage));
            Falloff = falloff ?? DamageFalloff.None;
            StructureRadius = SpecGuard.NonNegativeFinite(structureRadius, nameof(structureRadius));
            Explosion = explosion;
            StatusEffect = statusEffect;
            AreaEffect = areaEffect;
        }

        public int Damage { get; }

        public DamageFalloff Falloff { get; }

        public float StructureRadius { get; }

        public ExplosionSpec Explosion { get; }

        public StatusEffectSpec StatusEffect { get; }

        public AreaEffectSpec AreaEffect { get; }
    }
}
