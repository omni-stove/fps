using System;

namespace Shooter.Core
{
    public sealed class PenetrationMaterialSpec
    {
        public PenetrationMaterialSpec(string materialId, float penetrability)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                throw new ArgumentException("materialId must not be empty", nameof(materialId));
            }

            MaterialId = materialId.Trim();
            Penetrability = SpecGuard.NonNegativeFinite(penetrability, nameof(penetrability));
        }

        public string MaterialId { get; }

        public float Penetrability { get; }
    }
}
