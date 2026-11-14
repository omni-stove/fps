using System;

namespace Fps.Core
{
    /// <summary>
    /// 分隊への指示 (SquadCommand) の調整値。長押しとみなす秒数、照準の先を探す距離と敵に吸い付く角度、
    /// 分隊の無いモードで指示が届く範囲、付いていくときの距離。
    /// </summary>
    public sealed class SquadOrderSpec
    {
        public const float DefaultHoldSeconds = 0.5f;
        public const float DefaultMaxDistance = PingSpec.DefaultMaxDistance;
        public const float DefaultEnemyAimToleranceDegrees = PingSpec.DefaultEnemyAimToleranceDegrees;
        public const float DefaultNearbyRadius = 30f;
        public const float DefaultFollowDistance = 3f;
        public const float DefaultLeashDistance = 15f;

        public static SquadOrderSpec Default { get; } = new SquadOrderSpec();

        /// <param name="holdSeconds">押し続けてこれだけ経ったら長押し (付いてこさせる) にする秒数。0 より大きい有限値。</param>
        /// <param name="maxDistance">照準の先を探す距離 (m)。0 より大きい有限値。</param>
        /// <param name="enemyAimToleranceDegrees">照準が敵から外れていても敵を指したとみなす角度 (度)。0 以上 90 未満。</param>
        /// <param name="nearbyRadius">分隊の無いモードで、指示した人からこの距離 (m、水平) 以内の同じ陣営の bot に指示を出す。0 より大きい有限値。</param>
        /// <param name="followDistance">付いていくとき、指示した人からこれより離れたら寄っていく距離 (m、水平)。0 より大きい有限値。</param>
        /// <param name="leashDistance">付いていくとき、撃ち合っていてもこれより離れたら戻る距離 (m、水平)。followDistance 以上の有限値。</param>
        public SquadOrderSpec(
            float holdSeconds = DefaultHoldSeconds,
            float maxDistance = DefaultMaxDistance,
            float enemyAimToleranceDegrees = DefaultEnemyAimToleranceDegrees,
            float nearbyRadius = DefaultNearbyRadius,
            float followDistance = DefaultFollowDistance,
            float leashDistance = DefaultLeashDistance)
        {
            RequirePositive(holdSeconds, nameof(holdSeconds));
            RequirePositive(maxDistance, nameof(maxDistance));
            RequirePositive(nearbyRadius, nameof(nearbyRadius));
            RequirePositive(followDistance, nameof(followDistance));

            if (!(enemyAimToleranceDegrees >= 0f) || enemyAimToleranceDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyAimToleranceDegrees), enemyAimToleranceDegrees, "enemyAimToleranceDegrees must be at least 0 and less than 90");
            }

            if (!(leashDistance >= followDistance) || float.IsInfinity(leashDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leashDistance), leashDistance, "leashDistance must be finite and at least followDistance");
            }

            HoldSeconds = holdSeconds;
            MaxDistance = maxDistance;
            EnemyAimToleranceDegrees = enemyAimToleranceDegrees;
            NearbyRadius = nearbyRadius;
            FollowDistance = followDistance;
            LeashDistance = leashDistance;

            // 何を指したかの決め方は印 (Ping) と同じにし、PingAim をそのまま使う。寿命と数は PingAim が読まない。
            Aim = new PingSpec(maxDistance: maxDistance, enemyAimToleranceDegrees: enemyAimToleranceDegrees);
        }

        public float HoldSeconds { get; }

        public float MaxDistance { get; }

        public float EnemyAimToleranceDegrees { get; }

        public float NearbyRadius { get; }

        public float FollowDistance { get; }

        public float LeashDistance { get; }

        /// <summary><see cref="PingAim.Resolve"/> に渡す、届く距離と敵に吸い付く角度。</summary>
        public PingSpec Aim { get; }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
