using System;

namespace Shooter.Core
{
    public sealed class EnemySpec
    {
        public float SightRange { get; }

        public float AttackRange { get; }

        public float FireRange { get; }

        public float FireToleranceDegrees { get; }

        // 遠くから撃ち始めても近づくまではそうそう当たらないよう、FireRange に近いほど散らばりを足す。
        public float RangeSpreadDegrees { get; }

        public float LoseSightSeconds { get; }

        public float SearchSeconds { get; }

        public float ReactionSeconds { get; }

        public float OpeningSpreadDegrees { get; }

        public float SettledSpreadDegrees { get; }

        public int OpeningShots { get; }

        public float CoverHideSeconds { get; }

        public float CoverPeekSeconds { get; }

        public float CoverHealthFraction { get; }

        public EnemyArchetype Archetype { get; }

        public float StaggerResistance { get; }

        // 種類が違っても持たせておく。ボスの段階で種類だけを変えたときに、値を探し直さずに済むように。
        public ShieldSpec Shield { get; }

        public SniperSpec Sniper { get; }

        public ChargerSpec Charger { get; }

        public BomberSpec Bomber { get; }

        public float ArrivalDistance { get; }

        public float SprintRatio { get; }

        public float CrouchRatio { get; }

        public float RecoilControl { get; }

        // 屋内では壁が視線を切るので長くても困らず、開けた場所でも人が人に気づいて撃ち合い始める距離として不自然でない値にする。
        public const float DefaultSightRange = 40f;
        public const float DefaultAttackRange = 15f;

        // 見つけてから少し詰めた距離で構えて撃ち始め、DefaultAttackRange まで撃ちながら近づかせる。
        public const float DefaultFireRange = 30f;

        // 30m 先で半径 1m ほど散らばりが増え、人の幅には半分も当たらない。
        public const float DefaultRangeSpreadDegrees = 2f;

        public const float DefaultFireToleranceDegrees = 15f;

        public const float MaxFireToleranceDegrees = 180f;
        public const float DefaultLoseSightSeconds = 2f;
        public const float DefaultSearchSeconds = 6f;
        public const float DefaultReactionSeconds = 1.0f;

        // 撃ち始めの 1 発を外れやすくするため、15m 先で半径 1.3m ほどに散らす。
        public const float DefaultOpeningSpreadDegrees = 5f;

        public const float DefaultSettledSpreadDegrees = 0f;
        public const int DefaultOpeningShots = 3;
        public const float DefaultCoverHideSeconds = 1.5f;
        public const float DefaultCoverPeekSeconds = 2f;
        public const float DefaultCoverHealthFraction = 0.5f;
        public const float DefaultStaggerResistance = 0f;
        public const float DefaultArrivalDistance = 0.5f;

        // 敵ごとの歩きの速さはシーンが決めるので、プレイヤーの既定 (歩き 5・ダッシュ 8・しゃがみ 2.5 m/s) を比にして掛ける。
        public const float DefaultSprintRatio = 1.6f;
        public const float DefaultCrouchRatio = 0.5f;

        // プレイヤーは反動で上がる照準を手で引き戻すので、敵もたまった反動の大半を打ち消す。打ち消さないと連射の後半が頭上へ抜ける。
        public const float DefaultRecoilControl = 0.75f;

        public static float DefaultStaggerResistanceFor(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Rifleman:
                    return DefaultStaggerResistance;
                case EnemyArchetype.Shield:
                    return ShieldSpec.DefaultStaggerResistance;
                case EnemyArchetype.Sniper:
                    return SniperSpec.DefaultStaggerResistance;
                case EnemyArchetype.Charger:
                    return ChargerSpec.DefaultStaggerResistance;
                case EnemyArchetype.Bomber:
                    return BomberSpec.DefaultStaggerResistance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "archetype is not a defined EnemyArchetype");
            }
        }

        public static readonly EnemySpec Default = new EnemySpec(
            DefaultSightRange,
            DefaultAttackRange,
            DefaultLoseSightSeconds,
            DefaultSearchSeconds,
            DefaultReactionSeconds,
            DefaultOpeningSpreadDegrees,
            DefaultSettledSpreadDegrees,
            DefaultOpeningShots,
            DefaultCoverHideSeconds,
            DefaultCoverPeekSeconds,
            DefaultCoverHealthFraction,
            EnemyArchetype.Rifleman,
            ShieldSpec.Default,
            SniperSpec.Default,
            ChargerSpec.Default,
            BomberSpec.Default,
            DefaultStaggerResistance,
            DefaultFireRange,
            DefaultFireToleranceDegrees,
            DefaultRangeSpreadDegrees,
            DefaultArrivalDistance,
            DefaultSprintRatio,
            DefaultCrouchRatio,
            DefaultRecoilControl);

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
            ChargerSpec charger = null,
            BomberSpec bomber = null,
            float? staggerResistance = null,
            float fireRange = 0f,
            float fireToleranceDegrees = MaxFireToleranceDegrees,
            float rangeSpreadDegrees = 0f,
            float arrivalDistance = DefaultArrivalDistance,
            float sprintRatio = DefaultSprintRatio,
            float crouchRatio = DefaultCrouchRatio,
            float recoilControl = DefaultRecoilControl)
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
            RequireSpread(rangeSpreadDegrees, nameof(rangeSpreadDegrees));
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

            var resistance = staggerResistance ?? DefaultStaggerResistanceFor(archetype);
            RequireNonNegativeSeconds(resistance, nameof(staggerResistance));
            RequireNonNegativeSeconds(fireRange, nameof(fireRange));

            if (!(fireToleranceDegrees >= 0f) || fireToleranceDegrees > MaxFireToleranceDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fireToleranceDegrees), fireToleranceDegrees, "fireToleranceDegrees must be between 0 and " + MaxFireToleranceDegrees);
            }

            RequirePositive(arrivalDistance, nameof(arrivalDistance));
            RequirePositive(sprintRatio, nameof(sprintRatio));
            RequirePositive(crouchRatio, nameof(crouchRatio));
            if (!(recoilControl >= 0f) || recoilControl > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(recoilControl), recoilControl, "recoilControl must be between 0 and 1");
            }

            SightRange = sightRange;
            AttackRange = attackRange;
            // 視界を狭めた敵のデータに既定の撃ち始める距離が残っていても弾かず、視界の端から撃たせる。
            FireRange = Math.Min(Math.Max(fireRange, attackRange), sightRange);
            FireToleranceDegrees = fireToleranceDegrees;
            RangeSpreadDegrees = rangeSpreadDegrees;
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
            Charger = charger ?? ChargerSpec.Default;
            Bomber = bomber ?? BomberSpec.Default;
            StaggerResistance = resistance;
            ArrivalDistance = arrivalDistance;
            SprintRatio = sprintRatio;
            CrouchRatio = crouchRatio;
            RecoilControl = recoilControl;
        }

        // 散らす処理 (BlindFireShot.Spread) が受け付ける範囲に合わせる。
        public const float MaxSpreadDegrees = BlindFireShot.MaxSpreadDegrees;

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

        public float RangeSpreadDegreesAt(float distance)
        {
            if (!(distance > AttackRange) || FireRange <= AttackRange)
            {
                return 0f;
            }

            var t = Math.Min(1f, (distance - AttackRange) / (FireRange - AttackRange));
            return RangeSpreadDegrees * t;
        }

        private static void RequireSpread(float value, string name)
        {
            if (!(value >= 0f) || value > MaxSpreadDegrees)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and " + MaxSpreadDegrees);
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
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
