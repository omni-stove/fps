using System;

namespace Fps.Core
{
    public sealed class StaggerSpec
    {
        public const float DefaultEnemyFlinchSeconds = 0.5f;
        public const float DefaultEnemyKneelSeconds = 1.5f;
        public const float DefaultEnemyStumbleSeconds = 1f;
        public const float DefaultEnemyBlastSeconds = 1f;

        // 散弾の 1 粒のような弱い 1 発で毎回のけぞらせないよう、rifle の頭への 1 発 (50) には届き、弱い 1 発には届かない値にする。
        public const float DefaultEnemyHeadDamage = 20f;

        // rifle の脚への 1 発は距離で減っても 9 は残るので、遠くから脚を撃っても膝をつくよう、それより低くする。
        public const float DefaultEnemyLimbDamage = 8f;

        public const float DefaultEnemyMeleeDamage = 1f;
        public const float DefaultEnemyExplosionDamage = 1f;
        public const float DefaultEnemyImmunitySeconds = 2f;

        public const float DefaultPlayerStumbleSeconds = 0.4f;
        public const float DefaultPlayerBlastSeconds = 0.6f;
        public const float DefaultPlayerMeleeDamage = 1f;
        public const float DefaultPlayerExplosionDamage = 1f;
        public const float DefaultPlayerImmunitySeconds = 1f;
        public const float DefaultPlayerMoveMultiplier = 0.35f;
        public const float DefaultPlayerTurnMultiplier = 0.35f;

        public const float DefaultWindowSeconds = 1f;

        public static readonly StaggerSpec EnemyDefault = new StaggerSpec(
            DefaultEnemyFlinchSeconds,
            DefaultEnemyKneelSeconds,
            DefaultEnemyStumbleSeconds,
            DefaultEnemyBlastSeconds,
            DefaultEnemyHeadDamage,
            DefaultEnemyLimbDamage,
            DefaultEnemyMeleeDamage,
            DefaultEnemyExplosionDamage,
            DefaultWindowSeconds,
            DefaultEnemyImmunitySeconds);

        // プレイヤーは撃たれるたびに止まると操作を奪われ続けるので、銃弾では怯まず、近接と爆発でだけ短く怯む。
        public static readonly StaggerSpec PlayerDefault = new StaggerSpec(
            0f,
            0f,
            DefaultPlayerStumbleSeconds,
            DefaultPlayerBlastSeconds,
            DefaultEnemyHeadDamage,
            DefaultEnemyLimbDamage,
            DefaultPlayerMeleeDamage,
            DefaultPlayerExplosionDamage,
            DefaultWindowSeconds,
            DefaultPlayerImmunitySeconds,
            DefaultPlayerMoveMultiplier,
            DefaultPlayerTurnMultiplier);

        public float FlinchSeconds { get; }
        public float KneelSeconds { get; }
        public float StumbleSeconds { get; }
        public float BlastSeconds { get; }
        public float HeadDamage { get; }
        public float LimbDamage { get; }
        public float MeleeDamage { get; }
        public float ExplosionDamage { get; }
        public float WindowSeconds { get; }
        public float ImmunitySeconds { get; }
        public float MoveMultiplier { get; }
        public float TurnMultiplier { get; }

        // 怯みの長さを 0 にした種類は、その部位や種類で怯まないことを表す。
        public StaggerSpec(
            float flinchSeconds,
            float kneelSeconds,
            float stumbleSeconds,
            float blastSeconds,
            float headDamage,
            float limbDamage,
            float meleeDamage,
            float explosionDamage,
            float windowSeconds,
            float immunitySeconds,
            float moveMultiplier = 1f,
            float turnMultiplier = 1f)
        {
            RequireNonNegative(flinchSeconds, nameof(flinchSeconds));
            RequireNonNegative(kneelSeconds, nameof(kneelSeconds));
            RequireNonNegative(stumbleSeconds, nameof(stumbleSeconds));
            RequireNonNegative(blastSeconds, nameof(blastSeconds));
            RequireNonNegative(headDamage, nameof(headDamage));
            RequireNonNegative(limbDamage, nameof(limbDamage));
            RequireNonNegative(meleeDamage, nameof(meleeDamage));
            RequireNonNegative(explosionDamage, nameof(explosionDamage));
            if (!(windowSeconds > 0f) || float.IsInfinity(windowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "windowSeconds must be positive and finite");
            }

            RequireNonNegative(immunitySeconds, nameof(immunitySeconds));
            RequireFraction(moveMultiplier, nameof(moveMultiplier));
            RequireFraction(turnMultiplier, nameof(turnMultiplier));

            FlinchSeconds = flinchSeconds;
            KneelSeconds = kneelSeconds;
            StumbleSeconds = stumbleSeconds;
            BlastSeconds = blastSeconds;
            HeadDamage = headDamage;
            LimbDamage = limbDamage;
            MeleeDamage = meleeDamage;
            ExplosionDamage = explosionDamage;
            WindowSeconds = windowSeconds;
            ImmunitySeconds = immunitySeconds;
            MoveMultiplier = moveMultiplier;
            TurnMultiplier = turnMultiplier;
        }

        public float SecondsFor(StaggerKind kind)
        {
            switch (kind)
            {
                case StaggerKind.None:
                    return 0f;
                case StaggerKind.Flinch:
                    return FlinchSeconds;
                case StaggerKind.Kneel:
                    return KneelSeconds;
                case StaggerKind.Stumble:
                    return StumbleSeconds;
                case StaggerKind.Blast:
                    return BlastSeconds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined StaggerKind");
            }
        }

        public float DamageFor(StaggerKind kind)
        {
            switch (kind)
            {
                case StaggerKind.None:
                    return 0f;
                case StaggerKind.Flinch:
                    return HeadDamage;
                case StaggerKind.Kneel:
                    return LimbDamage;
                case StaggerKind.Stumble:
                    return MeleeDamage;
                case StaggerKind.Blast:
                    return ExplosionDamage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined StaggerKind");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }

        private static void RequireFraction(float value, string name)
        {
            if (!(value >= 0f) || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 1");
            }
        }
    }
}
