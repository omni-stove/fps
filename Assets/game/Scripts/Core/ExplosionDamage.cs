using System;

namespace Fps.Core
{
    /// <summary>
    /// 爆発 1 回が、中心から離れた相手に与えるダメージ。<see cref="FullDamageRadius"/> までは <see cref="MaxDamage"/>、
    /// そこから <see cref="Radius"/> まで線形に 0 へ減らし、<see cref="Radius"/> 以上と、間を遮られた相手には当たらない。
    /// 遮るかどうかの判定 (線を引く) は物理を持つ呼ぶ側が行い、結果だけを <see cref="At"/> に渡す。
    /// 生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class ExplosionDamage
    {
        /// <param name="maxDamage">中心 (<paramref name="fullDamageRadius"/> の内側) でのダメージ。0 以上。</param>
        /// <param name="radius">届く半径 (m)。0 より大きい有限値。</param>
        /// <param name="fullDamageRadius">減らさずに与える半径 (m)。0 以上で radius 未満。</param>
        public ExplosionDamage(int maxDamage, float radius, float fullDamageRadius = 0f)
        {
            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(fullDamageRadius >= 0f) || fullDamageRadius >= radius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullDamageRadius), fullDamageRadius, "fullDamageRadius must be non-negative and less than radius");
            }

            MaxDamage = maxDamage;
            Radius = radius;
            FullDamageRadius = fullDamageRadius;
        }

        /// <summary>中心でのダメージ。</summary>
        public int MaxDamage { get; }

        /// <summary>届く半径 (m)。ちょうどこの距離の相手には当たらない。</summary>
        public float Radius { get; }

        /// <summary>減らさずに <see cref="MaxDamage"/> を与える半径 (m)。</summary>
        public float FullDamageRadius { get; }

        /// <summary>中心から distance の相手に届くか (遮るものは見ない)。</summary>
        /// <param name="distance">中心から相手までの距離 (m)。0 以上 (正の無限大は届かない)。</param>
        public bool Reaches(float distance)
        {
            RequireDistance(distance);
            return distance < Radius;
        }

        /// <summary>
        /// 中心から distance の相手へのダメージ。obstructed なら 0。<see cref="Reaches"/> でなければ 0。
        /// 減衰の間は四捨五入 (0.5 は切り上げ) し、0 以上 <see cref="MaxDamage"/> 以下に収める。
        /// </summary>
        /// <param name="distance">中心から相手までの距離 (m)。0 以上 (正の無限大は届かない)。</param>
        /// <param name="obstructed">中心と相手の間を遮るものがあるか。</param>
        public int At(float distance, bool obstructed)
        {
            if (!Reaches(distance) || obstructed)
            {
                return 0;
            }

            if (distance <= FullDamageRadius)
            {
                return MaxDamage;
            }

            var t = (distance - (double)FullDamageRadius) / ((double)Radius - FullDamageRadius);
            var damage = (int)Math.Round(MaxDamage * (1d - t), MidpointRounding.AwayFromZero);
            return Math.Min(Math.Max(damage, 0), MaxDamage);
        }

        // At から逆算しないのは、整数に丸めたダメージでは MaxDamage が小さいときに強さが段になるため。
        public float StrengthAt(float distance)
        {
            if (!Reaches(distance))
            {
                return 0f;
            }

            if (distance <= FullDamageRadius)
            {
                return 1f;
            }

            var t = (distance - (double)FullDamageRadius) / ((double)Radius - FullDamageRadius);
            return (float)Math.Min(Math.Max(1d - t, 0d), 1d);
        }

        private static void RequireDistance(float distance)
        {
            if (!(distance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be negative or NaN");
            }
        }
    }
}
