using System;

namespace Fps.Core
{
    /// <summary>
    /// 印 (<see cref="PingMarker"/>) の寿命・同時に付けられる数・届く距離・敵に吸い付く角度。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class PingSpec
    {
        public const float DefaultLifetimeSeconds = 6f;

        public const int DefaultMaxMarkers = 3;

        public const float DefaultMaxDistance = 150f;

        public const float DefaultEnemyAimToleranceDegrees = 3f;

        public const float DefaultMinIntervalSeconds = 0.5f;

        /// <summary>すべて既定値の設定。</summary>
        public static PingSpec Default { get; } = new PingSpec();

        /// <param name="lifetimeSeconds">付けてから消えるまでの秒数。0 より大きい有限値。</param>
        /// <param name="maxMarkers">同時に付けられる数。1 以上。超えたら最も古い印を消す。</param>
        /// <param name="maxDistance">照準の先を探す距離 (m)。0 より大きい有限値。</param>
        /// <param name="enemyAimToleranceDegrees">照準が敵から外れていても敵に付ける角度 (度)。0 以上 90 未満。</param>
        /// <param name="minIntervalSeconds">続けて付けるときに空ける秒数。0 以上の有限値。</param>
        public PingSpec(
            float lifetimeSeconds = DefaultLifetimeSeconds,
            int maxMarkers = DefaultMaxMarkers,
            float maxDistance = DefaultMaxDistance,
            float enemyAimToleranceDegrees = DefaultEnemyAimToleranceDegrees,
            float minIntervalSeconds = DefaultMinIntervalSeconds)
        {
            if (!(lifetimeSeconds > 0f) || float.IsInfinity(lifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), lifetimeSeconds, "lifetimeSeconds must be positive and finite");
            }

            if (maxMarkers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMarkers), maxMarkers, "maxMarkers must be at least 1");
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            if (!(enemyAimToleranceDegrees >= 0f) || enemyAimToleranceDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyAimToleranceDegrees), enemyAimToleranceDegrees, "enemyAimToleranceDegrees must be at least 0 and less than 90");
            }

            if (!(minIntervalSeconds >= 0f) || float.IsInfinity(minIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minIntervalSeconds), minIntervalSeconds, "minIntervalSeconds must be at least 0 and finite");
            }

            LifetimeSeconds = lifetimeSeconds;
            MaxMarkers = maxMarkers;
            MaxDistance = maxDistance;
            EnemyAimToleranceDegrees = enemyAimToleranceDegrees;
            MinIntervalSeconds = minIntervalSeconds;
        }

        /// <summary>付けてから消えるまでの秒数。</summary>
        public float LifetimeSeconds { get; }

        /// <summary>同時に付けられる数。</summary>
        public int MaxMarkers { get; }

        /// <summary>照準の先を探す距離 (m)。</summary>
        public float MaxDistance { get; }

        /// <summary>照準が敵から外れていても敵に付ける角度 (度)。</summary>
        public float EnemyAimToleranceDegrees { get; }

        public float MinIntervalSeconds { get; }
    }
}
