using System;

namespace Fps.Core
{
    /// <summary>
    /// グレネード 1 種類のバランス値。<see cref="Id"/> で引き、信管・投げる間隔・初速と、信管が切れたときの効果 (<see cref="Impact"/>: 爆発・状態異常・範囲効果)、
    /// 投げたときのくっつき方 (<see cref="Stickiness"/>) を持つ。
    /// 時間は秒、速さは m/s、距離は m。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class GrenadeSpec
    {
        public const string DefaultId = "ff94f232-2647-4b77-a077-5693d8f954d1";

        public const string DefaultNameKey = "grenade.frag";

        /// <summary>生成時に指定しなかったときの <see cref="FuseSeconds"/>。</summary>
        public const float DefaultFuseSeconds = 2.5f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowSeconds"/>。</summary>
        public const float DefaultThrowSeconds = 0.6f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowSpeed"/>。</summary>
        public const float DefaultThrowSpeed = 14f;

        /// <summary>生成時に指定しなかったときの <see cref="ThrowLiftSpeed"/>。水平に狙っても弧を描いて遠くへ届く。</summary>
        public const float DefaultThrowLiftSpeed = 3f;

        /// <summary>生成時に指定しなかったときの <see cref="RollDamping"/>。地面に落ちてから 1 m 前後で止まる。</summary>
        public const float DefaultRollDamping = 4f;

        /// <summary>生成時に指定しなかったときの <see cref="MaxDamage"/>。標的ダミーとプレイヤーの最大体力 (100) を超え、近くなら 1 個で倒せる。</summary>
        public const int DefaultMaxDamage = 150;

        /// <summary>生成時に指定しなかったときの <see cref="DamageRadius"/>。</summary>
        public const float DefaultDamageRadius = 6f;

        /// <summary>生成時に指定しなかったときの <see cref="FullDamageRadius"/>。</summary>
        public const float DefaultFullDamageRadius = 1.5f;

        /// <summary>生成時に指定しなかったときの <see cref="StructureDamageAtCenter"/>。</summary>
        public const int DefaultStructureDamageAtCenter = 150;

        /// <summary>すべて既定値の Spec (破片手榴弾)。</summary>
        public static readonly GrenadeSpec Default = new GrenadeSpec();

        /// <param name="fuseSeconds">投げてから爆発するまでの秒数。0 以上の有限値。</param>
        /// <param name="throwSeconds">投げてから次を投げられ、武器を撃てるようになるまでの秒数。0 以上の有限値。</param>
        /// <param name="throwSpeed">視線の向きへの初速 (m/s)。0 以上の有限値。</param>
        /// <param name="throwLiftSpeed">初速に足す上向きの速さ (m/s)。0 以上の有限値。</param>
        /// <param name="maxDamage">中心 (<paramref name="fullDamageRadius"/> の内側) でのダメージ。0 以上。</param>
        /// <param name="damageRadius">爆発が届く半径 (m)。0 より大きい有限値。</param>
        /// <param name="fullDamageRadius">減らさずに maxDamage を与える半径 (m)。0 以上で damageRadius 未満。</param>
        /// <param name="structureDamageAtCenter">中心で建造物に与える被害。0 以上。半径は damageRadius。</param>
        /// <param name="id">小文字・ハイフン区切りの UUID。</param>
        /// <param name="nameKey">名前を引く表のキー。空なら名前は id。</param>
        /// <param name="stickiness">定義済みの値。</param>
        /// <param name="rollDamping">地面に触れている間の転がりの減り方 (1/s)。0 以上の有限値。</param>
        public GrenadeSpec(
            float fuseSeconds = DefaultFuseSeconds,
            float throwSeconds = DefaultThrowSeconds,
            float throwSpeed = DefaultThrowSpeed,
            float throwLiftSpeed = DefaultThrowLiftSpeed,
            int maxDamage = DefaultMaxDamage,
            float damageRadius = DefaultDamageRadius,
            float fullDamageRadius = DefaultFullDamageRadius,
            int structureDamageAtCenter = DefaultStructureDamageAtCenter,
            string id = DefaultId,
            string nameKey = DefaultNameKey,
            GrenadeStickiness stickiness = GrenadeStickiness.None,
            float rollDamping = DefaultRollDamping)
            : this(
                id,
                nameKey,
                ExplosiveImpact(maxDamage, damageRadius, fullDamageRadius, structureDamageAtCenter),
                fuseSeconds,
                throwSeconds,
                throwSpeed,
                throwLiftSpeed,
                stickiness,
                rollDamping)
        {
        }

        public GrenadeSpec(
            string id,
            string nameKey,
            ImpactSpec impact,
            float fuseSeconds = DefaultFuseSeconds,
            float throwSeconds = DefaultThrowSeconds,
            float throwSpeed = DefaultThrowSpeed,
            float throwLiftSpeed = DefaultThrowLiftSpeed,
            GrenadeStickiness stickiness = GrenadeStickiness.None,
            float rollDamping = DefaultRollDamping)
        {
            Id = SpecGuard.LowercaseUuid(id, nameof(id));
            NameKey = SpecGuard.NameKey(nameKey);
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            FuseSeconds = SpecGuard.NonNegativeFinite(fuseSeconds, nameof(fuseSeconds));
            ThrowSeconds = SpecGuard.NonNegativeFinite(throwSeconds, nameof(throwSeconds));
            ThrowSpeed = SpecGuard.NonNegativeFinite(throwSpeed, nameof(throwSpeed));
            ThrowLiftSpeed = SpecGuard.NonNegativeFinite(throwLiftSpeed, nameof(throwLiftSpeed));
            Stickiness = SpecGuard.Defined(stickiness, nameof(stickiness));
            RollDamping = SpecGuard.NonNegativeFinite(rollDamping, nameof(rollDamping));
        }

        public string Id { get; }

        public string NameKey { get; }

        public string LogName => NameKey.Length > 0 ? NameKey : Id;

        /// <summary>投げてから効果を起こすまでの秒数。</summary>
        public float FuseSeconds { get; }

        /// <summary>投げてから次を投げられ、武器を撃てるようになるまでの秒数。</summary>
        public float ThrowSeconds { get; }

        /// <summary>視線の向きへの初速 (m/s)。</summary>
        public float ThrowSpeed { get; }

        /// <summary>初速に足す上向きの速さ (m/s)。</summary>
        public float ThrowLiftSpeed { get; }

        // 投げ物は当たって効くのではなく信管で効くので、直撃のダメージ (ImpactSpec.Damage) は読まない
        public ImpactSpec Impact { get; }

        public GrenadeStickiness Stickiness { get; }

        /// <summary>
        /// 地面 (上を向いた面) に触れている間、水平の速さと回転を毎秒 e^-RollDamping 倍に減らす割合 (1/s)。0 なら物理の摩擦だけで転がり続ける。
        /// </summary>
        public float RollDamping { get; }

        /// <summary>爆発が人に与えるダメージ。爆発しない種類なら null。</summary>
        public ExplosionDamage Explosion => Impact.Explosion?.Damage;

        /// <summary>中心でのダメージ (<see cref="Explosion"/> の <see cref="ExplosionDamage.MaxDamage"/>)。爆発しない種類なら 0。</summary>
        public int MaxDamage => Explosion?.MaxDamage ?? 0;

        /// <summary>爆発が届く半径 (m)。建造物への被害もこの半径。爆発しない種類なら 0。</summary>
        public float DamageRadius => Explosion?.Radius ?? 0f;

        /// <summary>減らさずに <see cref="MaxDamage"/> を与える半径 (m)。爆発しない種類なら 0。</summary>
        public float FullDamageRadius => Explosion?.FullDamageRadius ?? 0f;

        /// <summary>中心で建造物に与える被害。半径は <see cref="DamageRadius"/>。爆発しない種類なら 0。</summary>
        public int StructureDamageAtCenter => Impact.Explosion?.StructureDamageAtCenter ?? 0;

        // ExplosionDamage の引数名 (radius) ではなくこの Spec の引数名で断り、調整値の JSON が項目名で示せるようにする。
        private static ImpactSpec ExplosiveImpact(int maxDamage, float damageRadius, float fullDamageRadius, int structureDamageAtCenter)
        {
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

            var explosion = new ExplosionSpec(
                new ExplosionDamage(maxDamage, damageRadius, fullDamageRadius), structureDamageAtCenter, DetonationTrigger.OnLifetimeEnd);
            return new ImpactSpec(damage: 0, explosion: explosion);
        }
    }
}
