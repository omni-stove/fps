using System;

namespace Fps.Core
{
    /// <summary>
    /// 命中 1 回分のダメージを計算する。結果は <see cref="Health.TakeDamage(int)"/> にそのまま渡せる。
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>頭への命中の倍率。</summary>
        public const double HeadMultiplier = 2.0;

        /// <summary>胴体への命中の倍率。</summary>
        public const double BodyMultiplier = 1.0;

        /// <summary>手足への命中の倍率。</summary>
        public const double LimbMultiplier = 0.75;

        /// <summary>部位の倍率を返す。</summary>
        public static double ZoneMultiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head:
                    return HeadMultiplier;
                case HitZone.Body:
                    return BodyMultiplier;
                case HitZone.Limb:
                    return LimbMultiplier;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown hit zone");
            }
        }

        /// <summary>
        /// baseDamage に部位倍率と距離減衰を掛け、四捨五入（0.5 は 0 から遠い方）した値を返す。
        /// baseDamage が 0 なら 0、それ以外は最低 1。int の最大値を超える分は切り詰める。
        /// </summary>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="zone">命中した部位。</param>
        /// <param name="distance">射手から命中点までの距離。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        public static int Calculate(int baseDamage, HitZone zone, float distance, DamageFalloff falloff)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            if (falloff == null)
            {
                throw new ArgumentNullException(nameof(falloff));
            }

            var zoneMultiplier = ZoneMultiplier(zone);
            var distanceMultiplier = falloff.MultiplierAt(distance);

            if (baseDamage == 0)
            {
                return 0;
            }

            var rounded = Math.Round(baseDamage * zoneMultiplier * distanceMultiplier, MidpointRounding.AwayFromZero);
            if (rounded >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)rounded);
        }
    }
}
