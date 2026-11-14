using System;

namespace Fps.Core
{
    /// <summary>
    /// 爆発する置き物 (樽・ガソリン缶など) 1 種類の値。耐久・爆発の威力と半径 (<see cref="Explosion"/>)・建造物への被害・
    /// 別の爆発で連鎖したときの遅れ・燃え始めてから爆発するまでの秒数を持つ。
    /// 時間は秒、距離は m。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class ExplosivePropSpec
    {
        /// <summary>生成時に指定しなかったときの <see cref="MaxHealth"/>。rifle の 1 発 (25) で爆発する。</summary>
        public const int DefaultMaxHealth = 20;

        /// <summary>生成時に指定しなかったときの <see cref="MaxDamage"/>。グレネード (<see cref="GrenadeSpec.DefaultMaxDamage"/>) と同じ。</summary>
        public const int DefaultMaxDamage = 150;

        /// <summary>生成時に指定しなかったときの <see cref="DamageRadius"/>。</summary>
        public const float DefaultDamageRadius = 5f;

        /// <summary>生成時に指定しなかったときの <see cref="FullDamageRadius"/>。</summary>
        public const float DefaultFullDamageRadius = 1.5f;

        /// <summary>生成時に指定しなかったときの <see cref="StructureDamageAtCenter"/>。</summary>
        public const int DefaultStructureDamageAtCenter = 150;

        /// <summary>生成時に指定しなかったときの <see cref="ChainDelaySeconds"/>。</summary>
        public const float DefaultChainDelaySeconds = 0.3f;

        /// <summary>生成時に指定しなかったときの <see cref="BurnSeconds"/>。0 なので燃えずにすぐ爆発する。</summary>
        public const float DefaultBurnSeconds = 0f;

        /// <summary>すべて既定値の Spec。</summary>
        public static readonly ExplosivePropSpec Default = new ExplosivePropSpec();

        /// <param name="maxHealth">耐久。被害でこれが 0 になると爆発する (燃えるなら燃え始める)。1 以上。</param>
        /// <param name="maxDamage">爆発の中心 (<paramref name="fullDamageRadius"/> の内側) で人に与えるダメージ。0 以上。</param>
        /// <param name="damageRadius">爆発が届く半径 (m)。0 より大きい有限値。建造物とほかの置き物への被害もこの半径。</param>
        /// <param name="fullDamageRadius">減らさずに maxDamage を与える半径 (m)。0 以上で damageRadius 未満。</param>
        /// <param name="structureDamageAtCenter">中心で建造物とほかの置き物に与える被害。0 以上。0 なら連鎖もしない。</param>
        /// <param name="chainDelaySeconds">ほかの置き物の爆発で耐久が 0 になったとき、爆発するまで待つ秒数の下限。0 以上の有限値。</param>
        /// <param name="burnSeconds">耐久が 0 になってから燃え続け、爆発するまでの秒数。0 以上の有限値。0 なら燃えない。</param>
        public ExplosivePropSpec(
            int maxHealth = DefaultMaxHealth,
            int maxDamage = DefaultMaxDamage,
            float damageRadius = DefaultDamageRadius,
            float fullDamageRadius = DefaultFullDamageRadius,
            int structureDamageAtCenter = DefaultStructureDamageAtCenter,
            float chainDelaySeconds = DefaultChainDelaySeconds,
            float burnSeconds = DefaultBurnSeconds)
        {
            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            if (!(damageRadius > 0f) || float.IsInfinity(damageRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(damageRadius), damageRadius, "damageRadius must be positive and finite");
            }

            // 同じ値を許すと、減衰の幅が 0 になり ExplosionDamage が距離で割れない。
            if (!(fullDamageRadius >= 0f) || fullDamageRadius >= damageRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullDamageRadius), fullDamageRadius, "fullDamageRadius must be non-negative and less than damageRadius");
            }

            if (structureDamageAtCenter < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(structureDamageAtCenter), structureDamageAtCenter, "structureDamageAtCenter must not be negative");
            }

            RequireNonNegativeFinite(chainDelaySeconds, nameof(chainDelaySeconds));
            RequireNonNegativeFinite(burnSeconds, nameof(burnSeconds));

            MaxHealth = maxHealth;
            StructureDamageAtCenter = structureDamageAtCenter;
            ChainDelaySeconds = chainDelaySeconds;
            BurnSeconds = burnSeconds;
            Explosion = new ExplosionDamage(maxDamage, damageRadius, fullDamageRadius);
        }

        /// <summary>耐久。</summary>
        public int MaxHealth { get; }

        /// <summary>中心で人に与えるダメージ (<see cref="Explosion"/> の <see cref="ExplosionDamage.MaxDamage"/>)。</summary>
        public int MaxDamage => Explosion.MaxDamage;

        /// <summary>爆発が届く半径 (m) (<see cref="Explosion"/> の <see cref="ExplosionDamage.Radius"/>)。建造物とほかの置き物への被害もこの半径。</summary>
        public float DamageRadius => Explosion.Radius;

        /// <summary>減らさずに <see cref="MaxDamage"/> を与える半径 (m) (<see cref="Explosion"/> の <see cref="ExplosionDamage.FullDamageRadius"/>)。</summary>
        public float FullDamageRadius => Explosion.FullDamageRadius;

        /// <summary>中心で建造物とほかの置き物に与える被害。半径は <see cref="DamageRadius"/>。</summary>
        public int StructureDamageAtCenter { get; }

        /// <summary>ほかの置き物の爆発で耐久が 0 になったとき、爆発するまで待つ秒数の下限。</summary>
        public float ChainDelaySeconds { get; }

        /// <summary>耐久が 0 になってから爆発するまで燃え続ける秒数。0 なら燃えない。</summary>
        public float BurnSeconds { get; }

        /// <summary>耐久が 0 になってから燃えてから爆発するか (<see cref="BurnSeconds"/> が 0 より大きいか)。</summary>
        public bool Burns => BurnSeconds > 0f;

        /// <summary>爆発が人に与えるダメージ (<see cref="MaxDamage"/>・<see cref="DamageRadius"/>・<see cref="FullDamageRadius"/>)。</summary>
        public ExplosionDamage Explosion { get; }

        /// <summary>
        /// 耐久が 0 になってから爆発するまでの秒数。ほかの置き物の爆発で 0 になった (chained) なら
        /// <see cref="BurnSeconds"/> と <see cref="ChainDelaySeconds"/> の長いほう、そうでなければ <see cref="BurnSeconds"/>。
        /// </summary>
        public float DetonationDelay(bool chained)
        {
            return chained ? Math.Max(BurnSeconds, ChainDelaySeconds) : BurnSeconds;
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
