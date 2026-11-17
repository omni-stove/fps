using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public enum CarrySource
    {
        None = 0,

        Held = 1,

        Carried = 2,
    }

    [Flags]
    public enum CarryGaits
    {
        None = 0,
        Walk = 1,
        Sprint = 2,
        Crouch = 4,
        Prone = 8,
        All = Walk | Sprint | Crouch | Prone,
    }

    public sealed class CarryLoadSpec
    {
        public const CarrySource DefaultWeightSource = CarrySource.Held;
        public const CarrySource DefaultMultiplierSource = CarrySource.Held;
        public const float DefaultFreeWeight = 0f;
        public const float DefaultSpeedLossPerKg = 0f;
        public const float DefaultMinWeightMultiplier = 0.5f;
        public const CarryGaits DefaultGaits = CarryGaits.All;

        public static readonly CarryLoadSpec Default = new CarryLoadSpec(
            DefaultWeightSource, DefaultMultiplierSource, DefaultFreeWeight, DefaultSpeedLossPerKg, DefaultMinWeightMultiplier, DefaultGaits);

        public CarryLoadSpec(
            CarrySource weightSource,
            CarrySource multiplierSource,
            float freeWeight,
            float speedLossPerKg,
            float minWeightMultiplier,
            CarryGaits gaits)
        {
            WeightSource = SpecGuard.Defined(weightSource, nameof(weightSource));
            MultiplierSource = SpecGuard.Defined(multiplierSource, nameof(multiplierSource));
            FreeWeight = SpecGuard.NonNegativeFinite(freeWeight, nameof(freeWeight));
            SpeedLossPerKg = SpecGuard.NonNegativeFinite(speedLossPerKg, nameof(speedLossPerKg));
            MinWeightMultiplier = SpecGuard.Fraction(minWeightMultiplier, nameof(minWeightMultiplier));
            if ((gaits & ~CarryGaits.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gaits), gaits, "gaits has an undefined flag");
            }

            Gaits = gaits;
        }

        public CarrySource WeightSource { get; }

        public CarrySource MultiplierSource { get; }

        public float FreeWeight { get; }

        public float SpeedLossPerKg { get; }

        public float MinWeightMultiplier { get; }

        public CarryGaits Gaits { get; }

        public float SpeedMultiplier(WeaponSpec held, IReadOnlyList<WeaponSpec> carried, float extraWeight = 0f)
        {
            var weight = Weight(held, carried) + (SpecGuard.IsNonNegativeFinite(extraWeight) ? extraWeight : 0f);
            var byWeight = Math.Max(MinWeightMultiplier, 1f - (Math.Max(0f, weight - FreeWeight) * SpeedLossPerKg));
            return byWeight * Multiplier(held, carried);
        }

        public float ForGait(CarryGaits gait, float speedMultiplier)
        {
            return (Gaits & gait) != 0 ? speedMultiplier : 1f;
        }

        private float Weight(WeaponSpec held, IReadOnlyList<WeaponSpec> carried)
        {
            switch (WeightSource)
            {
                case CarrySource.Held:
                    return held?.Weight ?? 0f;
                case CarrySource.Carried:
                    var total = 0f;
                    for (var i = 0; carried != null && i < carried.Count; i++)
                    {
                        total += carried[i]?.Weight ?? 0f;
                    }

                    return total;
                default:
                    return 0f;
            }
        }

        private float Multiplier(WeaponSpec held, IReadOnlyList<WeaponSpec> carried)
        {
            switch (MultiplierSource)
            {
                case CarrySource.Held:
                    return held?.MoveSpeedMultiplier ?? 1f;
                case CarrySource.Carried:
                    var product = 1f;
                    for (var i = 0; carried != null && i < carried.Count; i++)
                    {
                        product *= carried[i]?.MoveSpeedMultiplier ?? 1f;
                    }

                    return product;
                default:
                    return 1f;
            }
        }
    }
}
