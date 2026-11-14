using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 怯みの解除を EnemyAgent.Update より先に数え、解けたフレームの発砲を怯み中の発砲に数えないようにする。
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class StaggerReaction : MonoBehaviour
    {
        public const string LogTag = "[Stagger]";
        public const string AnimatorParameter = "Stagger";
        public const string InterruptReasonPrefix = "stagger-";

        [SerializeField, Min(0f), Tooltip("頭への被弾でのけぞる秒数。0 なら頭では怯まない")]
        private float _flinchSeconds = StaggerSpec.DefaultEnemyFlinchSeconds;

        [SerializeField, Min(0f), Tooltip("脚への被弾で膝をつく秒数。0 なら脚では怯まない")]
        private float _kneelSeconds = StaggerSpec.DefaultEnemyKneelSeconds;

        [SerializeField, Min(0f), Tooltip("近接でよろける秒数。0 なら近接では怯まない")]
        private float _stumbleSeconds = StaggerSpec.DefaultEnemyStumbleSeconds;

        [SerializeField, Min(0f), Tooltip("爆発で体勢を崩す秒数。0 なら爆発では怯まない")]
        private float _blastSeconds = StaggerSpec.DefaultEnemyBlastSeconds;

        [SerializeField, Min(0f), Tooltip("頭で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _headDamage = StaggerSpec.DefaultEnemyHeadDamage;

        [SerializeField, Min(0f), Tooltip("脚で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _limbDamage = StaggerSpec.DefaultEnemyLimbDamage;

        [SerializeField, Min(0f), Tooltip("近接で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _meleeDamage = StaggerSpec.DefaultEnemyMeleeDamage;

        [SerializeField, Min(0f), Tooltip("爆発で怯むのに短い間にたまっている必要のあるダメージ")]
        private float _explosionDamage = StaggerSpec.DefaultEnemyExplosionDamage;

        [SerializeField, Min(0.01f), Tooltip("ダメージをためておく秒数")]
        private float _windowSeconds = StaggerSpec.DefaultWindowSeconds;

        [SerializeField, Min(0f), Tooltip("怯みが解けてから再び怯むまでの秒数")]
        private float _immunitySeconds = StaggerSpec.DefaultEnemyImmunitySeconds;

        private Damageable _self;
        private EnemyAgent _agent;
        private StaggerJudge _judge;
        private int _shotsAtStart;
        private Animator _animator;
        private bool _animatorHasParameter;
        private bool _checkedParameter;
        private static readonly int _parameterHash = Animator.StringToHash(AnimatorParameter);

        // 敵ごとの取り付け口が無いので、ルールはプレイヤーへの取り付け (StaggerActionInstaller) がここに入れる。
        public static Stagger ActiveRule { get; set; } = Stagger.Off;

        public StaggerJudge Judge => _judge;

        public StaggerKind Current => _judge != null ? _judge.Current : StaggerKind.None;

        public bool IsStaggered => Current != StaggerKind.None;

        public bool IsBound => _judge != null;

        public static string FormatStaggerLog(string id, StaggerKind kind, float seconds, HitZone? zone, DamageKind damage)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} staggered kind={2} seconds={3:F2} zone={4} damage={5}",
                LogTag,
                id,
                kind,
                seconds,
                zone.HasValue ? zone.Value.ToString() : "none",
                damage);
        }

        public static string FormatResistLog(string id, StaggerKind kind)
        {
            return $"{LogTag} id={id} resisted kind={kind} reason=recovering";
        }

        // shots は怯んでいる間に撃った数。撃たないことを確かめる相手 (敵) のときだけ出す。
        public static string FormatRecoverLog(string id, StaggerKind kind, int? shots = null)
        {
            var line = $"{LogTag} id={id} recovered kind={kind}";
            return shots.HasValue ? line + " shots=" + shots.Value.ToString(CultureInfo.InvariantCulture) : line;
        }

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
                _immunitySeconds);
        }

        public void ApplySpec(StaggerSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_judge != null)
            {
                throw new InvalidOperationException($"StaggerReaction '{name}' is already bound; apply the spec before it starts");
            }

            _flinchSeconds = spec.FlinchSeconds;
            _kneelSeconds = spec.KneelSeconds;
            _stumbleSeconds = spec.StumbleSeconds;
            _blastSeconds = spec.BlastSeconds;
            _headDamage = spec.HeadDamage;
            _limbDamage = spec.LimbDamage;
            _meleeDamage = spec.MeleeDamage;
            _explosionDamage = spec.ExplosionDamage;
            _windowSeconds = spec.WindowSeconds;
            _immunitySeconds = spec.ImmunitySeconds;
        }

        // Edit Mode では Awake が走らないので、テストやシーン生成からも呼べるように分けておく。
        public bool Bind()
        {
            if (_judge != null)
            {
                return true;
            }

            _self = GetComponent<Damageable>();
            if (_self == null)
            {
                Debug.LogError($"{LogTag} '{name}' に Damageable がありません。", this);
                return false;
            }

            try
            {
                _judge = new StaggerJudge(CreateSpec());
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={_self.Id} の怯みの設定が不正です: {e.Message}", this);
                return false;
            }

            _agent = GetComponent<EnemyAgent>();
            _self.Damaged += OnDamaged;
            _self.Died += OnDied;
            _self.HealthReset += OnHealthReset;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_judge == null)
            {
                throw new InvalidOperationException($"StaggerReaction '{name}' is not bound");
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
            if (ActiveRule != Stagger.On || self.IsDead || _judge == null)
            {
                return;
            }

            var resistance = _agent != null ? _agent.StaggerResistance : 0f;
            var verdict = _judge.Hit(report.Zone, report.Kind, report.Amount, resistance);
            switch (verdict.Outcome)
            {
                case StaggerOutcome.Staggered:
                    Begin(verdict, report);
                    break;
                case StaggerOutcome.Immune:
                    Debug.Log(FormatResistLog(_self.Id, verdict.Kind), this);
                    break;
            }
        }

        private void Begin(StaggerVerdict verdict, DamageReport report)
        {
            if (_agent != null && _agent.IsInitialized)
            {
                _shotsAtStart = _agent.ShotCount;

                // 背後から倒すなど、もっと長く止めているものがあれば Interrupt は短い怯みで上書きしない。その間も怯みの見た目は出す。
                _agent.Interrupt(verdict.Seconds, InterruptReasonPrefix + verdict.Kind);
            }

            Debug.Log(FormatStaggerLog(_self.Id, verdict.Kind, verdict.Seconds, report.Zone, report.Kind), this);
            WriteAnimator(verdict.Kind);
        }

        private void Recover(StaggerKind kind)
        {
            int? shots = _agent != null && _agent.IsInitialized ? Math.Max(0, _agent.ShotCount - _shotsAtStart) : (int?)null;
            Debug.Log(FormatRecoverLog(_self.Id, kind, shots), this);
            WriteAnimator(StaggerKind.None);
        }

        private void OnDied(Damageable _)
        {
            _judge?.Reset();
            WriteAnimator(StaggerKind.None);
        }

        private void OnHealthReset(Health _)
        {
            _judge?.Reset();
            WriteAnimator(StaggerKind.None);
        }

        // リグは CharacterAnimator が Start で後から置くので、使うときに探す。値が無いコントローラーには渡さない (渡すと毎回警告が出る)。
        private void WriteAnimator(StaggerKind kind)
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                _checkedParameter = false;
            }

            if (_animator == null || !_animator.isActiveAndEnabled || !_animator.isInitialized)
            {
                return;
            }

            if (!_checkedParameter)
            {
                _animatorHasParameter = HasParameter(_animator);
                _checkedParameter = true;
            }

            if (_animatorHasParameter)
            {
                _animator.SetInteger(_parameterHash, (int)kind);
            }
        }

        private static bool HasParameter(Animator animator)
        {
            if (animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == _parameterHash && parameter.type == AnimatorControllerParameterType.Int)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
