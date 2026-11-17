using System;

namespace Shooter.Core
{
    public sealed class VehiclePartSpec
    {
        public VehiclePartSpec(string id, float damageMultiplier)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!(damageMultiplier >= 0f) || float.IsInfinity(damageMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageMultiplier), damageMultiplier, "damageMultiplier must not be negative and must be finite");
            }

            Id = id;
            DamageMultiplier = damageMultiplier;
        }

        public string Id { get; }

        public float DamageMultiplier { get; }
    }
}
