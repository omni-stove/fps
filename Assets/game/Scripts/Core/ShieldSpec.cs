using System;

namespace Fps.Core
{
    public sealed class ShieldSpec
    {
        public const float DefaultApproachSpeedMultiplier = 0.5f;
        public const float DefaultHoldDistance = 1.2f;
        public const float DefaultMeleeLowerSeconds = 1.5f;
        public const float DefaultExplosionLowerSeconds = 2.5f;

        public const int DefaultBashDamage = 35;
        public const float DefaultBashReach = 1.8f;
        public const float DefaultBashArcDegrees = 90f;
        public const float DefaultBashSwingSeconds = 0.7f;
        public const float DefaultBashCooldownSeconds = 1.2f;
        public const float DefaultBashImpactSeconds = 0.35f;
        public const float DefaultBashLungeDistance = 0.5f;

        public static readonly MeleeSpec DefaultBash = new MeleeSpec(
            DefaultBashDamage,
            DefaultBashReach,
            DefaultBashArcDegrees,
            DefaultBashSwingSeconds,
            DefaultBashCooldownSeconds,
            DefaultBashImpactSeconds,
            DefaultBashLungeDistance);

        public static readonly ShieldSpec Default = new ShieldSpec(
            DefaultApproachSpeedMultiplier,
            DefaultHoldDistance,
            DefaultMeleeLowerSeconds,
            DefaultExplosionLowerSeconds,
            DefaultBash);

        // 1 を超える値は受け付けない。盾を構えたまま普通より速く詰めると、盾を下ろさせる前に殴られて避けようがない。
        public float ApproachSpeedMultiplier { get; }

        public float HoldDistance { get; }

        public float MeleeLowerSeconds { get; }

        public float ExplosionLowerSeconds { get; }

        // プレイヤーの近接攻撃と同じ値の形にする。当たりの探し方 (MeleeStrike) を共有して、殴り合いの決まりを 1 つに保つため。
        public MeleeSpec Bash { get; }

        /// <exception cref="ArgumentNullException">bash が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。holdDistance は bash の届く距離以下。</exception>
        public ShieldSpec(
            float approachSpeedMultiplier,
            float holdDistance,
            float meleeLowerSeconds,
            float explosionLowerSeconds,
            MeleeSpec bash)
        {
            if (bash == null)
            {
                throw new ArgumentNullException(nameof(bash));
            }

            if (!(approachSpeedMultiplier > 0f) || approachSpeedMultiplier > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(approachSpeedMultiplier), approachSpeedMultiplier, "approachSpeedMultiplier must be greater than 0 and at most 1");
            }

            // 届かない距離で止まると、目の前で立ち尽くして一度も殴らない。
            if (!(holdDistance > 0f) || holdDistance > bash.Reach)
            {
                throw new ArgumentOutOfRangeException(nameof(holdDistance), holdDistance, "holdDistance must be positive and not exceed the bash reach");
            }

            RequireNonNegativeSeconds(meleeLowerSeconds, nameof(meleeLowerSeconds));
            RequireNonNegativeSeconds(explosionLowerSeconds, nameof(explosionLowerSeconds));

            ApproachSpeedMultiplier = approachSpeedMultiplier;
            HoldDistance = holdDistance;
            MeleeLowerSeconds = meleeLowerSeconds;
            ExplosionLowerSeconds = explosionLowerSeconds;
            Bash = bash;
        }

        // 銃弾で下ろさないのは、正面から撃つだけでは崩せない敵にするため。
        public float LowerSecondsFor(DamageKind kind)
        {
            switch (kind)
            {
                case DamageKind.Melee:
                    return MeleeLowerSeconds;
                case DamageKind.Explosion:
                    return ExplosionLowerSeconds;
                default:
                    return 0f;
            }
        }

        private static void RequireNonNegativeSeconds(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }
    }
}
