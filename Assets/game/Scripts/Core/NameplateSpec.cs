namespace Shooter.Core
{
    public sealed class NameplateSpec
    {
        public const float DefaultFriendlyRange = 60f;

        public const float DefaultEnemyRange = 35f;

        public static NameplateSpec Default { get; } = new NameplateSpec();

        // 味方の名前を出す最も遠い距離 (m)。0 なら味方には出さない
        public float FriendlyRange { get; }

        // 敵の名前を出す最も遠い距離 (m)。0 なら敵には出さない
        public float EnemyRange { get; }

        public NameplateSpec(float friendlyRange = DefaultFriendlyRange, float enemyRange = DefaultEnemyRange)
        {
            FriendlyRange = SpecGuard.NonNegativeFinite(friendlyRange, nameof(friendlyRange));
            EnemyRange = SpecGuard.NonNegativeFinite(enemyRange, nameof(enemyRange));
        }
    }
}
