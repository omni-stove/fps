using System;

namespace Shooter.Core
{
    public sealed class MineSpec
    {
        public const string DefaultNameKey = "deployable.mine";

        public const float DefaultArmSeconds = 1.5f;

        // 足元に置いて踏ませるので、隣を歩いただけで起きない程度に狭くする。
        public const float DefaultTriggerRadius = 1f;

        // 標的ダミーと敵の最大体力 (100) を超え、踏んだ相手を 1 個で倒せる。
        public const int DefaultMaxDamage = 150;

        public const float DefaultDamageRadius = 4f;

        public const float DefaultFullDamageRadius = 1.5f;

        public const int DefaultStructureDamageAtCenter = 100;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形なので、地雷の形と数はここに分けて持つ。
        public const int DefaultMaxActive = 3;

        public const int DefaultMaxHealth = 30;

        public const float DefaultWidth = 0.35f;

        public const float DefaultHeight = 0.1f;

        public const float DefaultDepth = 0.35f;

        public static MineSpec Default { get; } = new MineSpec(
            DefaultArmSeconds,
            DefaultTriggerRadius,
            new ExplosionSpec(
                new ExplosionDamage(DefaultMaxDamage, DefaultDamageRadius, DefaultFullDamageRadius), DefaultStructureDamageAtCenter));

        public MineSpec(float armSeconds, float triggerRadius, ExplosionSpec explosion)
        {
            ArmSeconds = SpecGuard.NonNegativeFinite(armSeconds, nameof(armSeconds));
            TriggerRadius = SpecGuard.PositiveFinite(triggerRadius, nameof(triggerRadius));
            Explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
        }

        // 置いた本人が置いてすぐ離れられるよう、これだけ経つまでは誰が入っても起きない。
        public float ArmSeconds { get; }

        public float TriggerRadius { get; }

        public ExplosionSpec Explosion { get; }
    }
}
