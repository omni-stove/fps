using System;

namespace Shooter.Core
{
    public sealed class BeaconSpec
    {
        public const string DefaultNameKey = "deployable.beacon";

        public const int DefaultUses = 3;

        // 足元ちょうどに出すとビーコンの当たり判定に体が重なるので、前 (+Z) へ離して出す。
        public const float DefaultSpawnDistance = 1f;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形なので、ビーコンの形と数はここに分けて持つ。
        public const int DefaultMaxActive = 1;

        public const int DefaultMaxHealth = 150;

        public const float DefaultWidth = 0.4f;

        public const float DefaultHeight = 1.2f;

        public const float DefaultDepth = 0.4f;

        public static BeaconSpec Default { get; } = new BeaconSpec(DefaultUses, DefaultSpawnDistance);

        public BeaconSpec(int uses, float spawnDistance)
        {
            if (uses < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(uses), uses, "uses must be at least 1");
            }

            if (!(spawnDistance >= 0f) || float.IsInfinity(spawnDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(spawnDistance), spawnDistance, "spawnDistance must not be negative and must be finite");
            }

            Uses = uses;
            SpawnDistance = spawnDistance;
        }

        public int Uses { get; }

        public float SpawnDistance { get; }
    }
}
