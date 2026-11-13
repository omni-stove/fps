using System;

namespace Fps.Core
{
    /// <summary>
    /// 着地 1 回分の落下ダメージを計算する (<see cref="GameRules.FallDamage"/> が On のとき)。
    /// 結果は <see cref="Health.TakeDamage(int)"/> にそのまま渡せる。オフラインとサーバーの両方が同じ式を使う。
    /// </summary>
    public static class FallDamageCalculator
    {
        /// <summary>
        /// 着地の下向きの速さ fallSpeed から落下ダメージを返す。
        /// <see cref="MotorSpec.SafeFallSpeed"/> 以下なら 0、<see cref="MotorSpec.LethalFallSpeed"/> 以上なら maxHealth、
        /// その間は maxHealth に比例して線形に増やし、端数は切り捨てる。
        /// </summary>
        /// <param name="fallSpeed">着地したときの下向きの速さ (m/s)。0 以上 (正の無限大は致死として扱う)。</param>
        /// <param name="spec">閾値を持つ移動の設定。</param>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        public static int Calculate(float fallSpeed, MotorSpec spec, int maxHealth)
        {
            if (!(fallSpeed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed), fallSpeed, "fallSpeed must not be negative or NaN");
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (fallSpeed <= spec.SafeFallSpeed)
            {
                return 0;
            }

            if (fallSpeed >= spec.LethalFallSpeed)
            {
                return maxHealth;
            }

            // 切り捨てにするのは、四捨五入だと LethalFallSpeed の手前でも満タンから倒れる値になり、
            // 「LethalFallSpeed 以上で倒れる」という閾値の意味が崩れるため。
            var t = (fallSpeed - (double)spec.SafeFallSpeed) / ((double)spec.LethalFallSpeed - spec.SafeFallSpeed);
            var damage = (int)Math.Floor(maxHealth * t);
            return Math.Min(Math.Max(damage, 0), maxHealth - 1);
        }
    }
}
