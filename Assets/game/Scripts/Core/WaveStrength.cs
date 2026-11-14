using System;

namespace Fps.Core
{
    /// <summary>
    /// 波の強さの倍率 (<see cref="Wave.StrengthMultiplier"/>) を敵に当てはめる。倍率が m のとき:
    /// <list type="bullet">
    /// <item>体力は m 倍 (四捨五入、1 以上)。</item>
    /// <item>撃ち始めるまでの反応時間 (<see cref="EnemySpec.ReactionSeconds"/>) は 1/m 倍。</item>
    /// <item>散らばり (<see cref="EnemySpec.OpeningSpreadDegrees"/>・<see cref="EnemySpec.SettledSpreadDegrees"/>) は 1/m 倍
    /// (<see cref="EnemySpec.MaxSpreadDegrees"/> で頭打ち)。</item>
    /// </list>
    /// 視認距離・攻撃距離・遮蔽の動き方は倍率で変えない。距離を伸ばすとレベルの作り (遮蔽物の配置) と噛み合わなくなるため。
    /// m が 1 なら元の値のまま、1 未満なら弱くなる。
    /// </summary>
    public static class WaveStrength
    {
        /// <summary>spec に倍率 multiplier を当てはめた設定。multiplier は 0 より大きい有限値。</summary>
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
                spec.StaggerResistance);
        }

        /// <summary>最大体力 baseHealth に倍率 multiplier を掛けた値。四捨五入し、1 以上 int の最大値以下に収める。</summary>
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
