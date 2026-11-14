using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "Fps/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("敵の種類")]
        private EnemyArchetype _archetype = EnemyArchetype.Rifleman;

        [Header("視界")]
        [SerializeField, Min(0.01f), Tooltip("目標に気づける距離 (m)")]
        private float _sightRange = EnemySpec.DefaultSightRange;

        [SerializeField, Min(0.01f), Tooltip("この距離 (m) 以内に目標が見えていれば撃ちに入る。視界の距離以下")]
        private float _attackRange = EnemySpec.DefaultAttackRange;

        [SerializeField, Min(0f), Tooltip("見失ってから探しに移るまでの秒数")]
        private float _loseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [SerializeField, Min(0f), Tooltip("探し続けてから巡回 (ルートが無ければ待機) に戻るまでの秒数")]
        private float _searchSeconds = EnemySpec.DefaultSearchSeconds;

        [Header("射撃")]
        [SerializeField, Min(0f), Tooltip("撃ちに入ってから最初に撃つまでの秒数")]
        private float _reactionSeconds = EnemySpec.DefaultReactionSeconds;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ちに入って最初の 1 発の散らばりの半角 (度)")]
        private float _openingSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始めの数発が過ぎた後の散らばりの半角 (度)")]
        private float _settledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [SerializeField, Min(0), Tooltip("散らばりを絞り切るまでの発数")]
        private int _openingShots = EnemySpec.DefaultOpeningShots;

        [Header("遮蔽")]
        [SerializeField, Min(0f), Tooltip("遮蔽点に着いてから、また隠れ直してから、覗くまで隠れている秒数")]
        private float _coverHideSeconds = EnemySpec.DefaultCoverHideSeconds;

        [SerializeField, Min(0f), Tooltip("遮蔽点から 1 回覗いている秒数")]
        private float _coverPeekSeconds = EnemySpec.DefaultCoverPeekSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("体力の割合がこれを下回ったら、撃たれていなくても遮蔽点へ移る。0 なら体力では移らない")]
        private float _coverHealthFraction = EnemySpec.DefaultCoverHealthFraction;

        [Header("怯み")]
        [SerializeField, Min(0f), Tooltip("怯みにくさ。0 で既定、1 で怯むのに 2 倍のダメージが要り、怯む長さは半分になる")]
        private float _staggerResistance = EnemySpec.DefaultStaggerResistance;

        [Header("種類ごとの値")]
        [SerializeField, Tooltip("種類が Shield のときに使う値")]
        private EnemyShieldSettings _shield = new EnemyShieldSettings();

        [SerializeField, Tooltip("種類が Sniper のときに使う値")]
        private EnemySniperSettings _sniper = new EnemySniperSettings();

        [SerializeField, Tooltip("種類が Charger のときに使う値")]
        private EnemyChargerSettings _charger = new EnemyChargerSettings();

        [SerializeField, Tooltip("種類が Bomber のときに使う値")]
        private EnemyBomberSettings _bomber = new EnemyBomberSettings();

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

        public EnemySpec ToSpec()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed enemy)" : name;
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
                    _staggerResistance);
            }
            catch (ArgumentException e)
            {
                throw new EnemyDefinitionException($"enemy '{source}' is invalid: {e.Message}", e);
            }
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
