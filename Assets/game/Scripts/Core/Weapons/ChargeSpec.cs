namespace Fps.Core
{
    public sealed class ChargeSpec
    {
        public ChargeSpec(float chargeSeconds, float minChargeFraction, float fullChargeDamageMultiplier, bool firesWhenFull = false)
        {
            ChargeSeconds = SpecGuard.PositiveFinite(chargeSeconds, nameof(chargeSeconds));
            MinChargeFraction = SpecGuard.Fraction(minChargeFraction, nameof(minChargeFraction));
            FullChargeDamageMultiplier = SpecGuard.NonNegativeFinite(fullChargeDamageMultiplier, nameof(fullChargeDamageMultiplier));
            FiresWhenFull = firesWhenFull;
        }

        public float ChargeSeconds { get; }

        public float MinChargeFraction { get; }

        public float FullChargeDamageMultiplier { get; }

        public bool FiresWhenFull { get; }
    }
}
