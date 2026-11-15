using System;

namespace Fps.Core
{
    public sealed class ExplosionSpec
    {
        public ExplosionSpec(
            ExplosionDamage damage,
            int structureDamageAtCenter = 0,
            DetonationTrigger detonation = DetonationTrigger.OnImpact)
        {
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            StructureDamageAtCenter = SpecGuard.NonNegative(structureDamageAtCenter, nameof(structureDamageAtCenter));
            Detonation = SpecGuard.Defined(detonation, nameof(detonation));
        }

        public ExplosionDamage Damage { get; }

        public int StructureDamageAtCenter { get; }

        public DetonationTrigger Detonation { get; }
    }
}
