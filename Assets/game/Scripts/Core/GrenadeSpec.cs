using System;

namespace Shooter.Core
{
    public sealed class GrenadeSpec
    {
        public const string DefaultId = "ff94f232-2647-4b77-a077-5693d8f954d1";

        public const string DefaultNameKey = "grenade.frag";

        public const float DefaultFuseSeconds = 2.5f;

        public const float DefaultThrowSeconds = 0.6f;

        public const float DefaultThrowSpeed = 14f;

        // 0 にしないのは、水平に狙っても弧を描いて遠くへ届くようにするため。
        public const float DefaultThrowLiftSpeed = 3f;

        // 地面に落ちてから 1 m 前後で止まる値にしてある。
        public const float DefaultRollDamping = 4f;

        // 標的ダミーとプレイヤーの最大体力 (100) を超え、近くなら 1 個で倒せるようにしてある。
        public const int DefaultMaxDamage = 150;

        public const float DefaultDamageRadius = 6f;

        public const float DefaultFullDamageRadius = 1.5f;

        public const int DefaultStructureDamageAtCenter = 150;

        public const float DefaultBodyRadius = 0.08f;

        public const float DefaultMass = 0.4f;

        public const float DefaultBounciness = 0.35f;

        public const float DefaultFriction = 0.6f;

        // Unity の Rigidbody の既定と同じにしてある。
        public const float DefaultAngularDamping = 0.05f;

        // 体のカプセル (半径 0.5m) の外に出すため、0.5 より長くしてある。
        public const float DefaultReleaseDistance = 0.7f;

        public static readonly GrenadeSpec Default = new GrenadeSpec();

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
            float rollDamping = DefaultRollDamping,
            float bodyRadius = DefaultBodyRadius,
            float mass = DefaultMass,
            float bounciness = DefaultBounciness,
            float friction = DefaultFriction,
            float angularDamping = DefaultAngularDamping,
            float releaseDistance = DefaultReleaseDistance)
            : this(
                id,
                nameKey,
                ExplosiveImpact(maxDamage, damageRadius, fullDamageRadius, structureDamageAtCenter),
                fuseSeconds,
                throwSeconds,
                throwSpeed,
                throwLiftSpeed,
                stickiness,
                rollDamping,
                bodyRadius,
                mass,
                bounciness,
                friction,
                angularDamping,
                releaseDistance)
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
            float rollDamping = DefaultRollDamping,
            float bodyRadius = DefaultBodyRadius,
            float mass = DefaultMass,
            float bounciness = DefaultBounciness,
            float friction = DefaultFriction,
            float angularDamping = DefaultAngularDamping,
            float releaseDistance = DefaultReleaseDistance)
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
            BodyRadius = SpecGuard.PositiveFinite(bodyRadius, nameof(bodyRadius));
            Mass = SpecGuard.PositiveFinite(mass, nameof(mass));
            Bounciness = SpecGuard.Fraction(bounciness, nameof(bounciness));
            Friction = SpecGuard.NonNegativeFinite(friction, nameof(friction));
            AngularDamping = SpecGuard.NonNegativeFinite(angularDamping, nameof(angularDamping));
            ReleaseDistance = SpecGuard.NonNegativeFinite(releaseDistance, nameof(releaseDistance));
        }

        public string Id { get; }

        public string NameKey { get; }

        public string LogName => NameKey.Length > 0 ? NameKey : Id;

        public float FuseSeconds { get; }

        public float ThrowSeconds { get; }

        public float ThrowSpeed { get; }

        public float ThrowLiftSpeed { get; }

        // 投げ物は当たって効くのではなく信管で効くので、直撃のダメージ (ImpactSpec.Damage) は読まない
        public ImpactSpec Impact { get; }

        public GrenadeStickiness Stickiness { get; }

        public float RollDamping { get; }

        public float BodyRadius { get; }

        public float Mass { get; }

        public float Bounciness { get; }

        public float Friction { get; }

        public float AngularDamping { get; }

        public float ReleaseDistance { get; }

        public ExplosionDamage Explosion => Impact.Explosion?.Damage;

        public int MaxDamage => Explosion?.MaxDamage ?? 0;

        public float DamageRadius => Explosion?.Radius ?? 0f;

        public float FullDamageRadius => Explosion?.FullDamageRadius ?? 0f;

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
