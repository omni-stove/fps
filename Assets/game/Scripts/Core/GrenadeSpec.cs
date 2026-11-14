using System;

namespace Fps.Core
{
    /// <summary>
    /// グレネード 1 種類のバランス値。信管・投げる間隔・初速と、爆発の威力と半径 (<see cref="Explosion"/>)・建造物への被害を持つ。
    /// 時間は秒、速さは m/s、距離は m。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class GrenadeSpec
    {
        /// <summary>生成時に指定しなかったときの <see cref="FuseSeconds"/>。</summary>
        public const float DefaultFuseSeconds = 2.5f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowSeconds"/>。</summary>
        public const float DefaultThrowSeconds = 0.6f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowSpeed"/>。</summary>
        public const float DefaultThrowSpeed = 14f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowLiftSpeed"/>。水平に狙っても弧を描いて遠くへ届く。</summary>
        public const float DefaultThrowLiftSpeed = 3f;

        /// <summary>生成時に指定しなかったときの <see cref="MaxDamage"/>。標的ダミーとプレイヤーの最大体力 (100) を超え、近くなら 1 個で倒せる。</summary>
        public const int DefaultMaxDamage = 150;

        /// <summary>生成時に指定しなかったときの <see cref="DamageRadius"/>。</summary>
        public const float DefaultDamageRadius = 6f;

        /// <summary>生成時に指定しなかったときの <see cref="FullDamageRadius"/>。</summary>
        public const float DefaultFullDamageRadius = 1.5f;

        /// <summary>生成時に指定しなかったときの <see cref="StructureDamageAtCenter"/>。</summary>
        public const int DefaultStructureDamageAtCenter = 150;

        /// <summary>すべて既定値の Spec。</summary>
        public static readonly GrenadeSpec Default = new GrenadeSpec();

        /// <param name="fuseSeconds">投げてから爆発するまでの秒数。0 以上の有限値。</param>
        /// <param name="throwSeconds">投げてから次を投げられ、武器を撃てるようになるまでの秒数。0 以上の有限値。</param>
        /// <param name="throwSpeed">視線の向きへの初速 (m/s)。0 以上の有限値。</param>
        /// <param name="throwLiftSpeed">初速に足す上向きの速さ (m/s)。0 以上の有限値。</param>
        /// <param name="maxDamage">中心 (<paramref name="fullDamageRadius"/> の内側) でのダメージ。0 以上。</param>
        /// <param name="damageRadius">爆発が届く半径 (m)。0 より大きい有限値。</param>
        /// <param name="fullDamageRadius">減らさずに maxDamage を与える半径 (m)。0 以上で damageRadius 未満。</param>
        /// <param name="structureDamageAtCenter">中心で建造物に与える被害。0 以上。半径は damageRadius。</param>
        public GrenadeSpec(
            float fuseSeconds = DefaultFuseSeconds,
            float throwSeconds = DefaultThrowSeconds,
            float throwSpeed = DefaultThrowSpeed,
            float throwLiftSpeed = DefaultThrowLiftSpeed,
            int maxDamage = DefaultMaxDamage,
            float damageRadius = DefaultDamageRadius,
            float fullDamageRadius = DefaultFullDamageRadius,
            int structureDamageAtCenter = DefaultStructureDamageAtCenter)
        {
            RequireNonNegativeFinite(fuseSeconds, nameof(fuseSeconds));
            RequireNonNegativeFinite(throwSeconds, nameof(throwSeconds));
            RequireNonNegativeFinite(throwSpeed, nameof(throwSpeed));
            RequireNonNegativeFinite(throwLiftSpeed, nameof(throwLiftSpeed));

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

            FuseSeconds = fuseSeconds;
            ThrowSeconds = throwSeconds;
            ThrowSpeed = throwSpeed;
            ThrowLiftSpeed = throwLiftSpeed;
            StructureDamageAtCenter = structureDamageAtCenter;
            Explosion = new ExplosionDamage(maxDamage, damageRadius, fullDamageRadius);
        }

        /// <summary>投げてから爆発するまでの秒数。</summary>
        public float FuseSeconds { get; }

        /// <summary>投げてから次を投げられ、武器を撃てるようになるまでの秒数。</summary>
        public float ThrowSeconds { get; }

        /// <summary>視線の向きへの初速 (m/s)。</summary>
        public float ThrowSpeed { get; }

        /// <summary>初速に足す上向きの速さ (m/s)。</summary>
        public float ThrowLiftSpeed { get; }

        /// <summary>中心でのダメージ (<see cref="Explosion"/> の <see cref="ExplosionDamage.MaxDamage"/>)。</summary>
        public int MaxDamage => Explosion.MaxDamage;

        /// <summary>爆発が届く半径 (m) (<see cref="Explosion"/> の <see cref="ExplosionDamage.Radius"/>)。建造物への被害もこの半径。</summary>
        public float DamageRadius => Explosion.Radius;

        /// <summary>減らさずに <see cref="MaxDamage"/> を与える半径 (m) (<see cref="Explosion"/> の <see cref="ExplosionDamage.FullDamageRadius"/>)。</summary>
        public float FullDamageRadius => Explosion.FullDamageRadius;

        /// <summary>中心で建造物に与える被害。半径は <see cref="DamageRadius"/>。</summary>
        public int StructureDamageAtCenter { get; }

        /// <summary>爆発が人に与えるダメージ (<see cref="MaxDamage"/>・<see cref="DamageRadius"/>・<see cref="FullDamageRadius"/>)。</summary>
        public ExplosionDamage Explosion { get; }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
