using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "Shooter/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        // 種類を選んだだけのデータが種類ごとの怯みにくさを使えるよう、「決めていない」を Inspector の 1 つの欄で表せる負の値にする。
        public const float ArchetypeStaggerResistance = -1f;

        [SerializeField, Tooltip("敵の種類")]
        private EnemyArchetype _archetype = EnemyArchetype.Rifleman;

        [Header("視界")]
        [SerializeField, Min(0.01f), Tooltip("目標に気づける距離 (m)")]
        private float _sightRange = EnemySpec.DefaultSightRange;

        [SerializeField, Min(0.01f), Tooltip("この距離 (m) 以内に目標が見えていれば撃ちに入る。視界の距離以下")]
        private float _attackRange = EnemySpec.DefaultAttackRange;

        [SerializeField, Min(0f), Tooltip("この距離 (m) 以内に目標が見えていれば、撃ちに入る距離の外でも近づきながら撃つ。撃ちに入る距離から視界の距離の間に収める")]
        private float _fireRange = EnemySpec.DefaultFireRange;

        [SerializeField, Min(0f), Tooltip("見失ってから探しに移るまでの秒数")]
        private float _loseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [SerializeField, Min(0f), Tooltip("探し続けてから巡回 (ルートが無ければ待機) に戻るまでの秒数")]
        private float _searchSeconds = EnemySpec.DefaultSearchSeconds;

        [Header("射撃")]
        [SerializeField, Min(0f), Tooltip("撃ちに入ってから最初に撃つまでの秒数")]
        private float _reactionSeconds = EnemySpec.DefaultReactionSeconds;

        [SerializeField, Range(0f, EnemySpec.MaxFireToleranceDegrees), Tooltip("体の向きと目標の向きの差 (度) がこれ以下になるまで撃たない")]
        private float _fireToleranceDegrees = EnemySpec.DefaultFireToleranceDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ちに入って最初の 1 発の散らばりの半角 (度)")]
        private float _openingSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始めの数発が過ぎた後の散らばりの半角 (度)")]
        private float _settledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始める距離にいる目標を撃つときに散らばりへ足す半角 (度)。撃ちに入る距離では 0")]
        private float _rangeSpreadDegrees = EnemySpec.DefaultRangeSpreadDegrees;

        [SerializeField, Min(0), Tooltip("散らばりを絞り切るまでの発数")]
        private int _openingShots = EnemySpec.DefaultOpeningShots;

        [SerializeField, Range(0f, 1f), Tooltip("たまった反動のうち、撃つたびに打ち消す割合。0 なら打ち消さず、1 なら反動で照準が上がらない")]
        private float _recoilControl = EnemySpec.DefaultRecoilControl;

        [Header("動き")]
        [SerializeField, Min(0.01f), Tooltip("行き先にこの水平距離 (m) まで近づいたら着いたとみなす")]
        private float _arrivalDistance = EnemySpec.DefaultArrivalDistance;

        [SerializeField, Min(0.01f), Tooltip("走るときに歩きの速さへ掛ける倍率")]
        private float _sprintRatio = EnemySpec.DefaultSprintRatio;

        [SerializeField, Min(0.01f), Tooltip("しゃがんで動くときに歩きの速さへ掛ける倍率")]
        private float _crouchRatio = EnemySpec.DefaultCrouchRatio;

        [SerializeField, Min(0f), Tooltip("狙う向きへ向き直る速さ (度/秒)")]
        private float _turnSpeed = EnemyAgent.DefaultTurnSpeed;

        [Header("体")]
        [SerializeField, Min(0f), Tooltip("立っているときの足元からの目の高さ (m)")]
        private float _eyeHeight = EnemyAgent.DefaultEyeHeight;

        [SerializeField, Min(0.01f), Tooltip("しゃがんだときの当たり判定の高さ (m)")]
        private float _crouchHeight = EnemyAgent.DefaultCrouchHeight;

        [SerializeField, Min(0f), Tooltip("しゃがんだときの足元からの目の高さ (m)")]
        private float _crouchEyeHeight = EnemyAgent.DefaultCrouchEyeHeight;

        [Header("遮蔽")]
        [SerializeField, Min(0f), Tooltip("遮蔽点に着いてから、また隠れ直してから、覗くまで隠れている秒数")]
        private float _coverHideSeconds = EnemySpec.DefaultCoverHideSeconds;

        [SerializeField, Min(0f), Tooltip("遮蔽点から 1 回覗いている秒数")]
        private float _coverPeekSeconds = EnemySpec.DefaultCoverPeekSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("体力の割合がこれを下回ったら、撃たれていなくても遮蔽点へ移る。0 なら体力では移らない")]
        private float _coverHealthFraction = EnemySpec.DefaultCoverHealthFraction;

        [Header("怯み")]
        [SerializeField, Min(ArchetypeStaggerResistance), Tooltip("怯みにくさ。0 で既定、1 で怯むのに 2 倍のダメージが要り、怯む長さは半分になる。負なら種類ごとの既定値")]
        private float _staggerResistance = ArchetypeStaggerResistance;

        [Header("種類ごとの値")]
        [SerializeField, Tooltip("種類が Shield のときに使う値")]
        private EnemyShieldSettings _shield = new EnemyShieldSettings();

        [SerializeField, Tooltip("種類が Sniper のときに使う値")]
        private EnemySniperSettings _sniper = new EnemySniperSettings();

        [SerializeField, Tooltip("種類が Charger のときに使う値")]
        private EnemyChargerSettings _charger = new EnemyChargerSettings();

        [SerializeField, Tooltip("種類が Bomber のときに使う値")]
        private EnemyBomberSettings _bomber = new EnemyBomberSettings();

        [Header("発見ゲージ")]
        [SerializeField, Tooltip("見つかるまでのゲージのたまり方・減り方と、物音の聞こえ方")]
        private EnemyAwarenessSettings _awareness = new EnemyAwarenessSettings();

        [Header("近接")]
        [SerializeField, Tooltip("近づいた目標を殴るときの値")]
        private EnemyMeleeSettings _melee = new EnemyMeleeSettings();

        public EnemyArchetype Archetype
        {
            get => _archetype;
            set => _archetype = value;
        }

        public float SightRange
        {
            get => _sightRange;
            set => _sightRange = value;
        }

        public float AttackRange
        {
            get => _attackRange;
            set => _attackRange = value;
        }

        public float FireRange
        {
            get => _fireRange;
            set => _fireRange = value;
        }

        public float FireToleranceDegrees
        {
            get => _fireToleranceDegrees;
            set => _fireToleranceDegrees = value;
        }

        public float RangeSpreadDegrees
        {
            get => _rangeSpreadDegrees;
            set => _rangeSpreadDegrees = value;
        }

        public float LoseSightSeconds
        {
            get => _loseSightSeconds;
            set => _loseSightSeconds = value;
        }

        public float SearchSeconds
        {
            get => _searchSeconds;
            set => _searchSeconds = value;
        }

        public float ReactionSeconds
        {
            get => _reactionSeconds;
            set => _reactionSeconds = value;
        }

        public float OpeningSpreadDegrees
        {
            get => _openingSpreadDegrees;
            set => _openingSpreadDegrees = value;
        }

        public float SettledSpreadDegrees
        {
            get => _settledSpreadDegrees;
            set => _settledSpreadDegrees = value;
        }

        public int OpeningShots
        {
            get => _openingShots;
            set => _openingShots = value;
        }

        public float CoverHideSeconds
        {
            get => _coverHideSeconds;
            set => _coverHideSeconds = value;
        }

        public float CoverPeekSeconds
        {
            get => _coverPeekSeconds;
            set => _coverPeekSeconds = value;
        }

        public float CoverHealthFraction
        {
            get => _coverHealthFraction;
            set => _coverHealthFraction = value;
        }

        public float StaggerResistance
        {
            get => _staggerResistance;
            set => _staggerResistance = value;
        }

        public float RecoilControl
        {
            get => _recoilControl;
            set => _recoilControl = value;
        }

        public float ArrivalDistance
        {
            get => _arrivalDistance;
            set => _arrivalDistance = value;
        }

        public float SprintRatio
        {
            get => _sprintRatio;
            set => _sprintRatio = value;
        }

        public float CrouchRatio
        {
            get => _crouchRatio;
            set => _crouchRatio = value;
        }

        /// <summary>狙う向きへ向き直る速さ (度/秒)。</summary>
        public float TurnSpeed
        {
            get => _turnSpeed;
            set => _turnSpeed = value;
        }

        /// <summary>立っているときの足元からの目の高さ (m)。</summary>
        public float EyeHeight
        {
            get => _eyeHeight;
            set => _eyeHeight = value;
        }

        /// <summary>しゃがんだときの当たり判定の高さ (m)。</summary>
        public float CrouchHeight
        {
            get => _crouchHeight;
            set => _crouchHeight = value;
        }

        /// <summary>しゃがんだときの足元からの目の高さ (m)。</summary>
        public float CrouchEyeHeight
        {
            get => _crouchEyeHeight;
            set => _crouchEyeHeight = value;
        }

        /// <summary>発見ゲージの値。<see cref="ToAwarenessSpec"/> がここから作る。</summary>
        public EnemyAwarenessSettings Awareness => _awareness;

        /// <summary>近接の値。<see cref="ToMeleeSpec"/> がここから作る。</summary>
        public EnemyMeleeSettings Melee => _melee;

        public EnemySpec ToSpec()
        {
            var source = SourceName;
            try
            {
                return new EnemySpec(
                    _sightRange,
                    _attackRange,
                    _loseSightSeconds,
                    _searchSeconds,
                    _reactionSeconds,
                    _openingSpreadDegrees,
                    _settledSpreadDegrees,
                    _openingShots,
                    _coverHideSeconds,
                    _coverPeekSeconds,
                    _coverHealthFraction,
                    _archetype,
                    _shield.ToSpec(),
                    _sniper.ToSpec(),
                    _charger.ToSpec(),
                    _bomber.ToSpec(),
                    StaggerResistanceOrArchetype(_staggerResistance),
                    _fireRange,
                    _fireToleranceDegrees,
                    _rangeSpreadDegrees,
                    _arrivalDistance,
                    _sprintRatio,
                    _crouchRatio,
                    _recoilControl);
            }
            catch (ArgumentException e)
            {
                throw new EnemyDefinitionException($"enemy '{source}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>発見ゲージの設定。値が不正なら <see cref="EnemyDefinitionException"/>。</summary>
        public AwarenessSpec ToAwarenessSpec()
        {
            try
            {
                return _awareness.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw new EnemyDefinitionException($"enemy '{SourceName}' has invalid awareness: {e.Message}", e);
            }
        }

        /// <summary>近接の設定。値が不正なら <see cref="EnemyDefinitionException"/>。</summary>
        public MeleeSpec ToMeleeSpec()
        {
            try
            {
                return _melee.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw new EnemyDefinitionException($"enemy '{SourceName}' has invalid melee: {e.Message}", e);
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed enemy)" : name;

        /// <summary>Inspector の怯みにくさ value を <see cref="EnemySpec"/> に渡す値にする。負なら null (種類ごとの既定値)。</summary>
        public static float? StaggerResistanceOrArchetype(float value)
        {
            return value < 0f ? (float?)null : value;
        }

        public void CopyFrom(EnemySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _archetype = spec.Archetype;
            _sightRange = spec.SightRange;
            _attackRange = spec.AttackRange;
            _fireRange = spec.FireRange;
            _fireToleranceDegrees = spec.FireToleranceDegrees;
            _rangeSpreadDegrees = spec.RangeSpreadDegrees;
            _loseSightSeconds = spec.LoseSightSeconds;
            _searchSeconds = spec.SearchSeconds;
            _reactionSeconds = spec.ReactionSeconds;
            _openingSpreadDegrees = spec.OpeningSpreadDegrees;
            _settledSpreadDegrees = spec.SettledSpreadDegrees;
            _openingShots = spec.OpeningShots;
            _coverHideSeconds = spec.CoverHideSeconds;
            _coverPeekSeconds = spec.CoverPeekSeconds;
            _coverHealthFraction = spec.CoverHealthFraction;
            _staggerResistance = spec.StaggerResistance;
            _arrivalDistance = spec.ArrivalDistance;
            _sprintRatio = spec.SprintRatio;
            _crouchRatio = spec.CrouchRatio;
            _recoilControl = spec.RecoilControl;
            _shield.CopyFrom(spec.Shield);
            _sniper.CopyFrom(spec.Sniper);
            _charger.CopyFrom(spec.Charger);
            _bomber.CopyFrom(spec.Bomber);
        }
    }

    // ArgumentException から派生させるのは、EnemyAgent.CreateSpec の不正な値を ArgumentException で受けている呼び出し側 (WaveSpawner など) を変えずに済ませるため。
    public sealed class EnemyDefinitionException : ArgumentException
    {
        public EnemyDefinitionException(string message)
            : base(message)
        {
        }

        public EnemyDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
