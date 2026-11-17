using System;

namespace Shooter.Core
{
    // 視認距離・攻撃距離・撃ち始める距離・遮蔽の動き方は倍率で変えない。距離を伸ばすとレベルの作り (遮蔽物の配置) と噛み合わなくなるため。
    public static class WaveStrength
    {
        public static EnemySpec Apply(EnemySpec spec, float multiplier)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            RequireMultiplier(multiplier);

            return new EnemySpec(
                spec.SightRange,
                spec.AttackRange,
                spec.LoseSightSeconds,
                spec.SearchSeconds,
                // ごく小さな倍率で割ると無限大になり EnemySpec が受け付けないので、有限の最大値で止める。
                Math.Min(float.MaxValue, spec.ReactionSeconds / multiplier),
                Math.Min(EnemySpec.MaxSpreadDegrees, spec.OpeningSpreadDegrees / multiplier),
                Math.Min(EnemySpec.MaxSpreadDegrees, spec.SettledSpreadDegrees / multiplier),
                spec.OpeningShots,
                spec.CoverHideSeconds,
                spec.CoverPeekSeconds,
                spec.CoverHealthFraction,
                spec.Archetype,
                spec.Shield,
                spec.Sniper,
                spec.Charger,
                spec.Bomber,
                spec.StaggerResistance,
                spec.FireRange,
                spec.FireToleranceDegrees,
                Math.Min(EnemySpec.MaxSpreadDegrees, spec.RangeSpreadDegrees / multiplier),
                spec.ArrivalDistance,
                spec.SprintRatio,
                spec.CrouchRatio,
                spec.RecoilControl);
        }

        public static int Health(int baseHealth, float multiplier)
        {
            if (baseHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth), baseHealth, "baseHealth must be at least 1");
            }

            RequireMultiplier(multiplier);

            var scaled = Math.Round((double)baseHealth * multiplier, MidpointRounding.AwayFromZero);
            return (int)Math.Max(1d, Math.Min(int.MaxValue, scaled));
        }

        private static void RequireMultiplier(float multiplier)
        {
            if (!(multiplier > 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be positive and finite");
            }
        }
    }
}
