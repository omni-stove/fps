using System;

namespace Shooter.Core
{
    public sealed class RemoteChargeSpec
    {
        public const string DefaultNameKey = "deployable.remoteCharge";

        // 標的ダミーと敵の最大体力 (100) を超え、貼り付けた相手を 1 個で倒せる。
        public const int DefaultMaxDamage = 150;

        public const float DefaultDamageRadius = 4.5f;

        public const float DefaultFullDamageRadius = 1.5f;

        public const int DefaultStructureDamageAtCenter = 150;

        // 置く操作 (押している間に狙って離す) と見分けられる長さにし、狙う前に離した押し方だけを起爆とみなす。
        public const float DefaultDetonateTapSeconds = 0.25f;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形と数なので、C4 の形と数はここに分けて持つ。
        // 貼りすぎて 1 回の起爆で何でも消せないよう、超えた分は古い物を消さずに置かせない。
        public const int DefaultMaxActive = 2;

        public const DeployOverflow DefaultOverflow = DeployOverflow.Refuse;

        public const int DefaultMaxHealth = 30;

        // 体から腕を伸ばして貼る距離。体の中心から測る。
        public const float DefaultMaxRange = 2.5f;

        public const float DefaultWidth = 0.25f;

        // 貼った面から手前への厚み。置く物の上 (+Y) を面の向きに合わせるので高さに入れる。
        public const float DefaultHeight = 0.08f;

        public const float DefaultDepth = 0.18f;

        public static RemoteChargeSpec Default { get; } = new RemoteChargeSpec(
            DefaultDetonateTapSeconds,
            new ExplosionSpec(
                new ExplosionDamage(DefaultMaxDamage, DefaultDamageRadius, DefaultFullDamageRadius), DefaultStructureDamageAtCenter));

        public RemoteChargeSpec(float detonateTapSeconds, ExplosionSpec explosion)
        {
            DetonateTapSeconds = SpecGuard.NonNegativeFinite(detonateTapSeconds, nameof(detonateTapSeconds));
            Explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
        }

        public float DetonateTapSeconds { get; }

        public ExplosionSpec Explosion { get; }
    }
}
