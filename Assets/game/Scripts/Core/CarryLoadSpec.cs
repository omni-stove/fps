using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>持っている武器のどれを数えるか。</summary>
    public enum CarrySource
    {
        /// <summary>数えない。</summary>
        None = 0,

        /// <summary>構えている武器 (持ち替え中は持ち替え先) だけ。</summary>
        Held = 1,

        /// <summary>持っている武器すべて。</summary>
        Carried = 2,
    }

    /// <summary>持ち物の倍率を掛ける動き。</summary>
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

    /// <summary>
    /// 武器の重さ (<see cref="WeaponSpec.Weight"/>)・持ち物の重さ (<see cref="InventoryRule.CountWeightInCarryLoad"/>) と武器ごとの倍率 (<see cref="WeaponSpec.MoveSpeedMultiplier"/>) から、移動の速さに掛ける倍率を決める設定。
    /// 倍率 = max(<see cref="MinWeightMultiplier"/>, 1 − max(0, 重さ − <see cref="FreeWeight"/>) × <see cref="SpeedLossPerKg"/>) × 武器ごとの倍率。
    /// </summary>
    public sealed class CarryLoadSpec
    {
        public const CarrySource DefaultWeightSource = CarrySource.Held;
        public const CarrySource DefaultMultiplierSource = CarrySource.Held;
        public const float DefaultFreeWeight = 0f;
        public const float DefaultSpeedLossPerKg = 0f;
        public const float DefaultMinWeightMultiplier = 0.5f;
        public const CarryGaits DefaultGaits = CarryGaits.All;

        /// <summary>重さでは遅くならず、構えている武器の倍率だけを歩き・ダッシュ・しゃがみ・伏せに掛ける。</summary>
        public static readonly CarryLoadSpec Default = new CarryLoadSpec(
            DefaultWeightSource, DefaultMultiplierSource, DefaultFreeWeight, DefaultSpeedLossPerKg, DefaultMinWeightMultiplier, DefaultGaits);

        /// <param name="weightSource">重さを数える武器。</param>
        /// <param name="multiplierSource">武器ごとの倍率を掛ける武器。Carried なら全部の積。</param>
        /// <param name="freeWeight">遅くならない重さ (kg)。0 以上の有限値。</param>
        /// <param name="speedLossPerKg"><paramref name="freeWeight"/> を超えた 1 kg ごとに倍率から引く量。0 以上の有限値。</param>
        /// <param name="minWeightMultiplier">重さで下がる倍率の下限。0 以上 1 以下。武器ごとの倍率はこの後に掛ける。</param>
        /// <param name="gaits">倍率を掛ける動き。</param>
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

        /// <summary>
        /// held を構え carried を持っているときの倍率。どちらも null なら持っていないものとして扱い、carried の null は飛ばす。
        /// extraWeight は武器でない持ち物の重さ (kg) で、<see cref="WeightSource"/> によらず数える。負や有限でない値は 0 と数える。
        /// </summary>
        public float SpeedMultiplier(WeaponSpec held, IReadOnlyList<WeaponSpec> carried, float extraWeight = 0f)
        {
            var weight = Weight(held, carried) + (SpecGuard.IsNonNegativeFinite(extraWeight) ? extraWeight : 0f);
            var byWeight = Math.Max(MinWeightMultiplier, 1f - (Math.Max(0f, weight - FreeWeight) * SpeedLossPerKg));
            return byWeight * Multiplier(held, carried);
        }

        /// <summary>gait の動きに掛ける倍率。<see cref="Gaits"/> に無い動きなら 1。</summary>
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
