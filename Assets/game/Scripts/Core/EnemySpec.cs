using System;

namespace Fps.Core
{
    /// <summary>
    /// 敵 AI の設定。距離はメートル、時間は秒。
    /// </summary>
    public sealed class EnemySpec
    {
        /// <summary>目標を視認できる距離。</summary>
        public float SightRange { get; }

        /// <summary>この距離以内に目標が見えていれば Attack になる。</summary>
        public float AttackRange { get; }

        /// <summary>見失ってから Search に移るまでの時間。</summary>
        public float LoseSightSeconds { get; }

        /// <summary>Search を続けてから Patrol（ルートが無ければ Idle）に戻るまでの時間。</summary>
        public float SearchSeconds { get; }

        /// <summary>発見してから最初に撃つまでの時間。</summary>
        public float ReactionSeconds { get; }

        /// <param name="sightRange">視認距離。0 より大きい。</param>
        /// <param name="attackRange">攻撃距離。0 より大きく sightRange 以下。</param>
        /// <param name="loseSightSeconds">見失ってから Search までの時間。0 以上。</param>
        /// <param name="searchSeconds">Search を続ける時間。0 以上。</param>
        /// <param name="reactionSeconds">発見から最初に撃つまでの時間。0 以上。</param>
        public EnemySpec(float sightRange, float attackRange, float loseSightSeconds, float searchSeconds, float reactionSeconds)
        {
            if (!(sightRange > 0f) || float.IsInfinity(sightRange))
            {
                throw new ArgumentOutOfRangeException(nameof(sightRange), sightRange, "sightRange must be positive and finite");
            }

            if (!(attackRange > 0f) || attackRange > sightRange)
            {
                throw new ArgumentOutOfRangeException(nameof(attackRange), attackRange, "attackRange must be positive and not exceed sightRange");
            }

            RequireNonNegativeSeconds(loseSightSeconds, nameof(loseSightSeconds));
            RequireNonNegativeSeconds(searchSeconds, nameof(searchSeconds));
            RequireNonNegativeSeconds(reactionSeconds, nameof(reactionSeconds));

            SightRange = sightRange;
            AttackRange = attackRange;
            LoseSightSeconds = loseSightSeconds;
            SearchSeconds = searchSeconds;
            ReactionSeconds = reactionSeconds;
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
