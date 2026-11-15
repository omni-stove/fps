namespace Fps.Core
{
    public sealed class ChargeSpec
    {
        public const float DefaultFullChargeSpeedMultiplier = 1f;

        public ChargeSpec(
            float chargeSeconds,
            float minChargeFraction,
            float fullChargeDamageMultiplier,
            bool firesWhenFull = false,
            bool firesBelowMin = false,
            float fullChargeSpeedMultiplier = DefaultFullChargeSpeedMultiplier)
        {
            ChargeSeconds = SpecGuard.PositiveFinite(chargeSeconds, nameof(chargeSeconds));
            MinChargeFraction = SpecGuard.Fraction(minChargeFraction, nameof(minChargeFraction));
            FullChargeDamageMultiplier = SpecGuard.NonNegativeFinite(fullChargeDamageMultiplier, nameof(fullChargeDamageMultiplier));
            FiresWhenFull = firesWhenFull;
            FiresBelowMin = firesBelowMin;
            FullChargeSpeedMultiplier = SpecGuard.PositiveFinite(fullChargeSpeedMultiplier, nameof(fullChargeSpeedMultiplier));
        }

        public float ChargeSeconds { get; }

        public float MinChargeFraction { get; }

        public float FullChargeDamageMultiplier { get; }

        public bool FiresWhenFull { get; }

        public bool FiresBelowMin { get; }

        public float FullChargeSpeedMultiplier { get; }
    }
}
