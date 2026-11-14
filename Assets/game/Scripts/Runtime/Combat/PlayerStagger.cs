using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerStagger : MonoBehaviour, IMovementOverride
    {
        // はしごやジップラインに掴まっている間は、怯んでも手を離させない。そちらの横取りを優先させるため最も低くする。
        public const int MovementPriority = int.MinValue;

        [SerializeField, Min(0f), Tooltip("頭への被弾でのけぞる秒数。0 なら頭では怯まない")]
        private float _flinchSeconds;

        [SerializeField, Min(0f), Tooltip("脚への被弾で膝をつく秒数。0 なら脚では怯まない")]
        private float _kneelSeconds;

        [SerializeField, Min(0f), Tooltip("近接でよろける秒数。0 なら近接では怯まない")]
        private float _stumbleSeconds = StaggerSpec.DefaultPlayerStumbleSeconds;

        [SerializeField, Min(0f), Tooltip("爆発で体勢を崩す秒数。0 なら爆発では怯まない")]
        private float _blastSeconds = StaggerSpec.DefaultPlayerBlastSeconds;

        [SerializeField, Min(0f), Tooltip("頭で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _headDamage = StaggerSpec.DefaultEnemyHeadDamage;

        [SerializeField, Min(0f), Tooltip("脚で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _limbDamage = StaggerSpec.DefaultEnemyLimbDamage;

        [SerializeField, Min(0f), Tooltip("近接で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _meleeDamage = StaggerSpec.DefaultPlayerMeleeDamage;

        [SerializeField, Min(0f), Tooltip("爆発で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _explosionDamage = StaggerSpec.DefaultPlayerExplosionDamage;

        [SerializeField, Min(0.01f), Tooltip("ダメージをためておく秒数")]
        private float _windowSeconds = StaggerSpec.DefaultWindowSeconds;

        [SerializeField, Min(0f), Tooltip("怯みが解けてから再び怯むまでの秒数")]
        private float _immunitySeconds = StaggerSpec.DefaultPlayerImmunitySeconds;

        [SerializeField, Range(0f, 1f), Tooltip("怯んでいる間の移動の速さの倍率")]
        private float _moveMultiplier = StaggerSpec.DefaultPlayerMoveMultiplier;

        [SerializeField, Range(0f, 1f), Tooltip("怯んでいる間の視点の回りの倍率")]
        private float _turnMultiplier = StaggerSpec.DefaultPlayerTurnMultiplier;

        private PlayerController _mover;
        private Damageable _self;
        private StaggerJudge _judge;
        private bool _registered;
        private FirstPersonLook _slowedLook;
        private Stagger _rule = Stagger.Off;

        public Stagger Rule
        {
            get => _rule;
            set
            {
                if (!Enum.IsDefined(typeof(Stagger), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Stagger");
                }

                _rule = value;

                // 怯んでいる最中に切ったら、その場で解いて操作を返す。
                if (value == Stagger.Off && _judge != null && _judge.IsStaggered)
                {
                    var kind = _judge.Current;
                    _judge.Reset();
                    Recover(kind);
                }
            }
        }

        public StaggerJudge Judge => _judge;

        public StaggerKind Current => _judge != null ? _judge.Current : StaggerKind.None;

        public bool IsStaggered => Current != StaggerKind.None;

        public bool IsBound => _judge != null;

        int IMovementOverride.Priority => MovementPriority;

        bool IMovementOverride.IsActive => IsStaggered;

        PlayerActionConditions IMovementOverride.Conditions => PlayerActionConditions.Staggered;

        public StaggerSpec CreateSpec()
        {
            return new StaggerSpec(
                _flinchSeconds,
                _kneelSeconds,
                _stumbleSeconds,
                _blastSeconds,
                _headDamage,
                _limbDamage,
                _meleeDamage,
                _explosionDamage,
                _windowSeconds,
                _immunitySeconds,
                _moveMultiplier,
                _turnMultiplier);
        }

        // Edit Mode では Awake が走らないので、テストからも呼べるように分けておく。
        public bool Bind()
        {
            if (_judge != null)
            {
                return true;
            }

            _mover = GetComponent<PlayerController>();
            _self = GetComponent<Damageable>();
            if (_mover == null || _self == null)
            {
                Debug.LogError($"{StaggerReaction.LogTag} '{name}' に PlayerController と Damageable が必要です。", this);
                return false;
            }

            try
            {
                _judge = new StaggerJudge(CreateSpec());
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{StaggerReaction.LogTag} id={_self.Id} の怯みの設定が不正です: {e.Message}", this);
                return false;
            }

            _self.Damaged += OnDamaged;
            _self.Died += OnDied;
            _self.HealthReset += OnHealthReset;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_judge == null)
            {
                throw new InvalidOperationException($"PlayerStagger '{name}' is not bound");
            }

            var ended = _judge.Tick(deltaSeconds);
            if (ended != StaggerKind.None)
            {
                Recover(ended);
            }
        }

        private void Awake()
        {
            if (!Bind())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_mover != null && !_registered)
            {
                _registered = _mover.RegisterMovementOverride(this);
            }
        }

        private void OnDisable()
        {
            if (_registered && _mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }

            _registered = false;
            RestoreLook();
        }

        private void OnDestroy()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_self, null))
            {
                _self.Damaged -= OnDamaged;
                _self.Died -= OnDied;
                _self.HealthReset -= OnHealthReset;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            if (_rule != Stagger.On || self.IsDead || _judge == null)
            {
                return;
            }

            var verdict = _judge.Hit(report.Zone, report.Kind, report.Amount, 0f);
            switch (verdict.Outcome)
            {
                case StaggerOutcome.Staggered:
                    Debug.Log(StaggerReaction.FormatStaggerLog(_self.Id, verdict.Kind, verdict.Seconds, report.Zone, report.Kind), this);
                    SlowLook();
                    break;
                case StaggerOutcome.Immune:
                    Debug.Log(StaggerReaction.FormatResistLog(_self.Id, verdict.Kind), this);
                    break;
            }
        }

        private void Recover(StaggerKind kind)
        {
            RestoreLook();
            Debug.Log(StaggerReaction.FormatRecoverLog(_self.Id, kind), this);
        }

        private void OnDied(Damageable _)
        {
            _judge?.Reset();
            RestoreLook();
        }

        private void OnHealthReset(Health _)
        {
            _judge?.Reset();
            RestoreLook();
        }

        private void SlowLook()
        {
            var look = _mover != null ? _mover.Look : null;
            if (look == null)
            {
                return;
            }

            look.TurnMultiplier = _judge.Spec.TurnMultiplier;
            _slowedLook = look;
        }

        private void RestoreLook()
        {
            if (_slowedLook != null)
            {
                _slowedLook.TurnMultiplier = 1f;
            }

            _slowedLook = null;
        }

        void IMovementOverride.Begin(PlayerController mover)
        {
        }

        // 通常の移動の計算 (重力・接地) をそのまま使い、入力だけを弱めて遅く歩かせる。ダッシュとジャンプはさせない。
        MovementOverrideStep IMovementOverride.Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var motor = mover.Motor;
            var yawRadians = mover.Look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var move = input.Move * _judge.Spec.MoveMultiplier;
            var displacement = motor.Step(
                move.ToNumerics(), yawRadians, false, mover.IsCrouched, false, input.IsGrounded, deltaSeconds).ToUnity();
            return MovementOverrideStep.Move(displacement, motor.Velocity.ToUnity());
        }

        void IMovementOverride.End(PlayerController mover)
        {
        }
    }
}
