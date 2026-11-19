namespace Shooter.Core
{
    public sealed class SquadSpawnSpec
    {
        public const float DefaultMinEnemyDistance = 20f;
        public const float DefaultDamageCooldownSeconds = 5f;
        public const float DefaultMateDistance = 2f;
        public const float DefaultClearance = 1f;
        public const int DefaultSlotCount = 8;

        public static SquadSpawnSpec Default { get; } = new SquadSpawnSpec();

        public SquadSpawnSpec(
            float minEnemyDistance = DefaultMinEnemyDistance,
            float damageCooldownSeconds = DefaultDamageCooldownSeconds,
            float mateDistance = DefaultMateDistance,
            float clearance = DefaultClearance,
            int slotCount = DefaultSlotCount)
        {
            MinEnemyDistance = SpecGuard.NonNegativeFinite(minEnemyDistance, nameof(minEnemyDistance));
            DamageCooldownSeconds = SpecGuard.NonNegativeFinite(damageCooldownSeconds, nameof(damageCooldownSeconds));
            // 0 を許すと出る位置が味方の体と重なり、空きの判定で必ず弾かれて分隊からの復活が決して選ばれなくなる。
            MateDistance = SpecGuard.PositiveFinite(mateDistance, nameof(mateDistance));
            Clearance = SpecGuard.NonNegativeFinite(clearance, nameof(clearance));
            SlotCount = SpecGuard.AtLeast(slotCount, 1, nameof(slotCount));
        }

        public float MinEnemyDistance { get; }

        public float DamageCooldownSeconds { get; }

        public float MateDistance { get; }

        public float Clearance { get; }

        public int SlotCount { get; }
    }
}
