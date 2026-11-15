using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class PenetrationSpec
    {
        public PenetrationSpec(
            int maxPenetrations, float damageMultiplierPerPenetration, float maxThickness, IReadOnlyList<PenetrationMaterialSpec> materials = null)
        {
            MaxPenetrations = SpecGuard.AtLeast(maxPenetrations, 1, nameof(maxPenetrations));
            DamageMultiplierPerPenetration = SpecGuard.Fraction(damageMultiplierPerPenetration, nameof(damageMultiplierPerPenetration));
            MaxThickness = SpecGuard.NonNegativeFinite(maxThickness, nameof(maxThickness));
            Materials = CopyMaterials(materials);
        }

        public int MaxPenetrations { get; }

        public float DamageMultiplierPerPenetration { get; }

        public float MaxThickness { get; }

        // 並びに無い素材は抜けやすさ 1 (MaxThickness のまま)。
        public IReadOnlyList<PenetrationMaterialSpec> Materials { get; }

        public float PenetrabilityOf(string surfaceMaterialId)
        {
            foreach (var material in Materials)
            {
                if (string.Equals(material.MaterialId, surfaceMaterialId, StringComparison.Ordinal))
                {
                    return material.Penetrability;
                }
            }

            return 1f;
        }

        private static PenetrationMaterialSpec[] CopyMaterials(IReadOnlyList<PenetrationMaterialSpec> materials)
        {
            if (materials == null || materials.Count == 0)
            {
                return Array.Empty<PenetrationMaterialSpec>();
            }

            var copy = new PenetrationMaterialSpec[materials.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var material = materials[i] ?? throw new ArgumentException("materials must not contain null", nameof(materials));
                if (!ids.Add(material.MaterialId))
                {
                    throw new ArgumentException("materials lists '" + material.MaterialId + "' more than once", nameof(materials));
                }

                copy[i] = material;
            }

            return copy;
        }
    }
}
