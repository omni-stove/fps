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

        /// <summary>Attack に入ってから最初に撃つまでの時間。</summary>
        public float ReactionSeconds { get; }

        /// <summary>Attack に入って最初の 1 発の散らばりの半角 (度)。</summary>
        public float OpeningSpreadDegrees { get; }

        /// <summary>撃ち始めの数発が過ぎた後の散らばりの半角 (度)。</summary>
        public float SettledSpreadDegrees { get; }

        /// <summary>OpeningSpreadDegrees から SettledSpreadDegrees まで絞るのにかける発数。0 なら最初から SettledSpreadDegrees。</summary>
        public int OpeningShots { get; }

        /// <summary>TakeCover で遮蔽点に着いてから、また隠れ直してから、覗くまでしゃがんでいる時間。</summary>
        public float CoverHideSeconds { get; }

        /// <summary>TakeCover で 1 回覗いている時間。覗き始めてから ReactionSeconds 経つまでは撃たない。</summary>
        public float CoverPeekSeconds { get; }

        /// <summary>体力の割合がこれを下回ったら、撃たれていなくても TakeCover に移る。0 なら体力では移らない。</summary>
        public float CoverHealthFraction { get; }

        public EnemyArchetype Archetype { get; }

        public float StaggerResistance { get; }

        // 種類が違っても持たせておく。ボスの段階で種類だけを変えたときに、値を探し直さずに済むように。
        public ShieldSpec Shield { get; }

        public SniperSpec Sniper { get; }

        public const float DefaultCoverHideSeconds = 1.5f;
        public const float DefaultCoverPeekSeconds = 2f;
        public const float DefaultCoverHealthFraction = 0.5f;

        /// <param name="sightRange">視認距離。0 より大きい。</param>
        /// <param name="attackRange">攻撃距離。0 より大きく sightRange 以下。</param>
        /// <param name="loseSightSeconds">見失ってから Search までの時間。0 以上。</param>
        /// <param name="searchSeconds">Search を続ける時間。0 以上。</param>
        /// <param name="reactionSeconds">Attack に入ってから最初に撃つまでの時間。0 以上。</param>
        /// <param name="openingSpreadDegrees">Attack に入って最初の 1 発の散らばりの半角 (度)。0 以上 <see cref="MaxSpreadDegrees"/> 以下。</param>
        /// <param name="settledSpreadDegrees">撃ち始めの数発が過ぎた後の散らばりの半角 (度)。0 以上 <see cref="MaxSpreadDegrees"/> 以下。</param>
        /// <param name="openingShots">散らばりを絞るのにかける発数。0 以上。</param>
        /// <param name="coverHideSeconds">TakeCover でしゃがんで隠れている時間。0 以上。</param>
        /// <param name="coverPeekSeconds">TakeCover で 1 回覗いている時間。0 以上。</param>
        /// <param name="coverHealthFraction">体力の割合がこれを下回ったら TakeCover に移る。0 以上 1 以下。</param>
        /// <param name="archetype">敵の種類。</param>
        /// <param name="shield">盾持ちの値。null なら既定値。</param>
        /// <param name="sniper">狙撃兵の値。null なら既定値。</param>
        /// <param name="staggerResistance">怯みにくさ (<see cref="StaggerJudge.Threshold"/>・<see cref="StaggerJudge.Duration"/>)。0 以上の有限値。</param>
        public EnemySpec(
            float sightRange,
            float attackRange,
            float loseSightSeconds,
            float searchSeconds,
            float reactionSeconds,
            float openingSpreadDegrees = 0f,
            float settledSpreadDegrees = 0f,
            int openingShots = 0,
            float coverHideSeconds = DefaultCoverHideSeconds,
            float coverPeekSeconds = DefaultCoverPeekSeconds,
            float coverHealthFraction = DefaultCoverHealthFraction,
            EnemyArchetype archetype = EnemyArchetype.Rifleman,
            ShieldSpec shield = null,
            SniperSpec sniper = null,
            float staggerResistance = 0f)
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
            RequireSpread(openingSpreadDegrees, nameof(openingSpreadDegrees));
            RequireSpread(settledSpreadDegrees, nameof(settledSpreadDegrees));
            if (openingShots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(openingShots), openingShots, "openingShots must be non-negative");
            }

            RequireNonNegativeSeconds(coverHideSeconds, nameof(coverHideSeconds));
            RequireNonNegativeSeconds(coverPeekSeconds, nameof(coverPeekSeconds));
            if (!(coverHealthFraction >= 0f) || coverHealthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(coverHealthFraction), coverHealthFraction, "coverHealthFraction must be between 0 and 1");
            }

            if (!Enum.IsDefined(typeof(EnemyArchetype), archetype))
            {
                throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "archetype is not a defined EnemyArchetype");
            }

            RequireNonNegativeSeconds(staggerResistance, nameof(staggerResistance));

            SightRange = sightRange;
            AttackRange = attackRange;
            LoseSightSeconds = loseSightSeconds;
            SearchSeconds = searchSeconds;
            ReactionSeconds = reactionSeconds;
            OpeningSpreadDegrees = openingSpreadDegrees;
            SettledSpreadDegrees = settledSpreadDegrees;
            OpeningShots = openingShots;
            CoverHideSeconds = coverHideSeconds;
            CoverPeekSeconds = coverPeekSeconds;
            CoverHealthFraction = coverHealthFraction;
            Archetype = archetype;
            Shield = shield ?? ShieldSpec.Default;
            Sniper = sniper ?? SniperSpec.Default;
            StaggerResistance = staggerResistance;
        }

        /// <summary>散らばりの半角の上限 (度)。散らす処理 (<see cref="BlindFireShot.Spread(System.Numerics.Vector3, float, System.Random)"/>) が受け付ける範囲に合わせる。</summary>
        public const float MaxSpreadDegrees = BlindFireShot.MaxSpreadDegrees;

        /// <summary>
        /// Attack に入ってから shotIndex 発目 (0 始まり) の散らばりの半角 (度)。
        /// 0 発目は OpeningSpreadDegrees で、OpeningShots 発かけて SettledSpreadDegrees まで一定の幅で絞り、以後はそのまま。
        /// </summary>
        public float SpreadDegreesForShot(int shotIndex)
        {
            if (shotIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shotIndex), shotIndex, "shotIndex must be non-negative");
            }

            if (shotIndex >= OpeningShots)
            {
                return SettledSpreadDegrees;
            }

            var t = (float)shotIndex / OpeningShots;
            return OpeningSpreadDegrees + ((SettledSpreadDegrees - OpeningSpreadDegrees) * t);
        }

        private static void RequireSpread(float value, string name)
        {
            if (!(value >= 0f) || value > MaxSpreadDegrees)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and " + MaxSpreadDegrees);
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
