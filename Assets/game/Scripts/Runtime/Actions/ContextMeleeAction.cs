using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 近接の入力 (MeleeController) で、相手と自分の状態に合う技 (ContextMeleeSelector) があればふつうの近接の代わりに出す。
    // 技は「位置を合わせる → 双方の操作を止めたまま演出の秒数を待つ → 結果を出す」の順に進め、演出は IContextMeleePresenter に任せる。
    // 使えるかは近接のルール (Melee) に従う。技だけを切りたいゲームは IncludeFinisher を切るか Selector から外す。
    [DisallowMultipleComponent]
    public sealed class ContextMeleeAction : MonoBehaviour, IWeaponModifier
    {
        public const string InterruptReasonPrefix = "context-melee-";

        [SerializeField, Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("相手の背後とみなす、相手の真後ろを中心にした扇の全体の角度 (度)")]
        private float _behindArcDegrees = ContextMeleeSelector.DefaultBehindArcDegrees;

        [SerializeField, Tooltip("怯んだ相手への処刑技を登録するか。値は MeleeController の近接の値 (MeleeSpec) から作る")]
        private bool _includeFinisher = true;

        private readonly ContextMeleeSelector _selector = new ContextMeleeSelector();
        private readonly ContextMeleeRun _run = new ContextMeleeRun();
        private MeleeController _melee;
        private Damageable _self;
        private Melee _rule = Melee.Off;
        private MeleeSpec _finisherSource;
        private MeleeSpec _searchSpec;
        private MeleeSpec _searchSource;
        private float _searchReach;

        private Damageable _target;
        private EnemyAgent _targetAgent;
        private string _priorReason = string.Empty;
        private float _priorSeconds;
        private float _priorTime;
        private PlayerController _targetMover;
        private IContextMeleePresenter _presenter;
        private ControlHandoff _claimedMover;
        private ControlHandoff _claimedWeapon;
        private ControlHandoff _claimedTarget;
        private FirstPersonLook _look;
        private bool _lookWasSuspended;
        private bool _addedModifier;
        private Vector3 _alignFrom;
        private Vector3 _alignTo;
        private float _yawFrom;
        private float _yawTo;

        public ContextMeleeSelector Selector => _selector;

        public ContextMeleeRun Run => _run;

        public Melee Rule => _rule;

        public bool IsPerforming => _run.IsActive;

        public Damageable Target => _run.IsActive ? _target : null;

        public MeleeController MeleeController => _melee;

        public bool IsBound => _melee != null && _melee.IsInitialized;

        // null なら同じ GameObject で IContextMeleePresenter を実装したコンポーネントを探す (無ければ演出なし)。
        public IContextMeleePresenter Presenter { get; set; }

        public float BehindArcDegrees
        {
            get => _behindArcDegrees;
            set => _behindArcDegrees = Mathf.Clamp(value, 1f, MeleeSpec.MaxArcDegrees);
        }

        public bool IncludeFinisher
        {
            get => _includeFinisher;
            set
            {
                _includeFinisher = value;
                SyncFinisher();
            }
        }

        public string AttackerId => _self != null ? _self.Id : name;

        bool IWeaponModifier.BlocksFire => IsPerforming;

        bool IWeaponModifier.BlocksReload => IsPerforming;

        float IWeaponModifier.SpreadMultiplier => 1f;

        float IWeaponModifier.ModifyConeDegrees(WeaponSpec spec, float coneDegrees) => coneDegrees;

        float IWeaponModifier.ModifySpreadFraction(float spreadFraction) => spreadFraction;

        // MeleeController の Awake がまだのこともあるので、ここではエラーにせず、ルールを受ける Configure で結び直す。
        private void Awake()
        {
            var melee = GetComponent<MeleeController>();
            if (melee == null || !melee.IsInitialized || !Bind())
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Stop(ContextMeleeInterruption.Cancelled, true);
        }

        private void OnDestroy()
        {
            Stop(ContextMeleeInterruption.Cancelled, true);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Edit Mode では Awake が走らないので、テストや取り付けからも呼べるように分けておく。
        public bool Bind()
        {
            if (IsBound)
            {
                return true;
            }

            _melee = GetComponent<MeleeController>();
            if (_melee == null || !_melee.IsInitialized)
            {
                Debug.LogError($"{ContextMeleeLog.LogTag} '{name}' に初期化済みの MeleeController がありません。技は近接の入力から出します。", this);
                _melee = null;
                return false;
            }

            _self = GetComponent<Damageable>();
            SyncFinisher();
            return true;
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rule = rules.Actions.Melee;
            enabled = _rule == Melee.On && Bind();

            // Edit Mode では enabled を変えても OnDisable が走らないので、技の取り消しはここでも揃える。
            if (!enabled)
            {
                Stop(ContextMeleeInterruption.Cancelled, true);
            }
        }

        // 相手が見つからないか、合う技が無ければ false。呼ぶ側 (MeleeController) はそのときふつうの近接を出す。
        // 振れない理由 (MeleeRefusal) のログはふつうの近接の側で出すので、ここでは黙って false を返す。
        public bool TryBegin()
        {
            if (!enabled || _rule != Melee.On || _run.IsActive || !Bind())
            {
                return false;
            }

            if (_melee.CheckSwing() != MeleeRefusal.None)
            {
                return false;
            }

            SyncFinisher();
            var spec = SearchSpec();
            if (spec == null)
            {
                return false;
            }

            if (!MeleeStrike.TryFind(transform, transform.position, _melee.AimForward(), spec, _melee.HitMask, out var hit))
            {
                return false;
            }

            var situation = new ContextMeleeSituation(Observe(hit.Target), Mathf.Max(0f, hit.Distance));
            var move = _selector.Select(situation);
            if (move == null)
            {
                Debug.Log(ContextMeleeLog.NoMove(AttackerId, hit.Target.Id, situation.Conditions), this);
                return false;
            }

            Begin(move, hit.Target);
            return true;
        }

        public ContextMeleeConditions Observe(Damageable target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var conditions = ContextMeleeConditions.None;
            var enemyStagger = target.GetComponent<StaggerReaction>();
            var playerStagger = target.GetComponent<PlayerStagger>();
            if ((enemyStagger != null && enemyStagger.IsStaggered) || (playerStagger != null && playerStagger.IsStaggered))
            {
                conditions |= ContextMeleeConditions.TargetStaggered;
            }

            if (target.IsDowned)
            {
                conditions |= ContextMeleeConditions.TargetDowned;
            }

            var targetTransform = target.transform;
            if (ContextMeleeSelector.IsBehind(
                targetTransform.position.ToNumerics(), targetTransform.forward.ToNumerics(), transform.position.ToNumerics(), _behindArcDegrees))
            {
                conditions |= ContextMeleeConditions.TargetBehind;
            }

            // プレイヤーは常にこちらを見ている前提にする。見ているかを知る口が無いので、気づいていないと誤って出すよりよい。
            var agent = target.GetComponent<EnemyAgent>();
            if (agent != null && agent.IsInitialized && (agent.State == EnemyState.Idle || agent.State == EnemyState.Patrol))
            {
                conditions |= ContextMeleeConditions.TargetUnaware;
            }

            var mover = _melee != null ? _melee.Mover : GetComponent<PlayerController>();
            if (mover != null)
            {
                if (mover.IsGrounded)
                {
                    conditions |= ContextMeleeConditions.SelfGrounded;
                }

                if (mover.IsCrouched)
                {
                    conditions |= ContextMeleeConditions.SelfCrouching;
                }

                if (mover.IsSprinting)
                {
                    conditions |= ContextMeleeConditions.SelfSprinting;
                }
            }

            return conditions;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_run.IsActive)
            {
                return;
            }

            if (_self != null && (_self.IsDead || _self.IsDowned))
            {
                Stop(ContextMeleeInterruption.AttackerDown, true);
                return;
            }

            if (_target == null || _target.IsDead)
            {
                Stop(ContextMeleeInterruption.TargetLost, true);
                return;
            }

            var move = _run.Move;
            var events = _run.Tick(Mathf.Max(0f, deltaSeconds));
            Align(_run.IsActive ? _run.AlignProgress : 1f);
            if ((events & ContextMeleeEvents.Resolved) != 0)
            {
                Resolve(move);
            }
        }

        // 中断できない技なら止めずに false。倒れたなどで必ず止めるときは force。
        public bool Interrupt(ContextMeleeInterruption reason, bool force = false)
        {
            if (!_run.IsActive)
            {
                return false;
            }

            if (!force && !_run.Move.Interruptible)
            {
                return false;
            }

            Stop(reason, true);
            return true;
        }

        private void Begin(ContextMeleeMove move, Damageable target)
        {
            _run.Begin(move);
            _target = target;
            _targetAgent = target.GetComponent<EnemyAgent>();
            _targetMover = target.GetComponent<PlayerController>();
            _presenter = Presenter ?? GetComponent<IContextMeleePresenter>();

            HoldControls(move);

            _alignFrom = transform.position;
            _alignTo = ContextMeleeSelector.StandPoint(
                transform.position.ToNumerics(), target.transform.position.ToNumerics(), target.transform.forward.ToNumerics(), move.StandDistance).ToUnity();
            _yawFrom = _look != null && _look.IsInitialized ? _look.Yaw : transform.eulerAngles.y;
            var toTarget = target.transform.position - _alignTo;
            _yawTo = toTarget.x * toTarget.x + toTarget.z * toTarget.z > 1e-6f ? Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg : _yawFrom;

            if (_self != null)
            {
                _self.Damaged += OnSelfDamaged;
            }

            target.Died += OnTargetDied;

            Debug.Log(ContextMeleeLog.Started(AttackerId, target.Id, move.Id), this);
            _presenter?.OnStarted(gameObject, target, move);

            // 位置合わせの秒数が 0 の技は、始めたフレームで合わせ終える。
            if (move.AlignSeconds <= 0f)
            {
                Align(1f);
            }
        }

        private void HoldControls(ContextMeleeMove move)
        {
            var mover = _melee.Mover;
            if (mover.Handoff.Claim(this))
            {
                _claimedMover = mover.Handoff;
            }

            // オフラインでは移動と武器が同じ口を共有している (GameBootstrap) ので、別のときだけ武器の口も握る。
            var weaponHandoff = _melee.Weapon.Handoff;
            if (weaponHandoff != null && !ReferenceEquals(weaponHandoff, mover.Handoff) && weaponHandoff.Claim(this))
            {
                _claimedWeapon = weaponHandoff;
            }

            _addedModifier = _melee.Weapon.AddModifier(this);

            _look = mover.Look;
            if (_look != null)
            {
                _lookWasSuspended = _look.InputSuspended;
                _look.InputSuspended = true;
            }

            var ads = GetComponent<AimDownSights>();
            if (ads != null)
            {
                ads.Release();
            }

            if (_targetAgent != null && _targetAgent.IsInitialized)
            {
                // 結果を出すフレームまで止めておく。秒数ちょうどだと敵の Update が先に解き、とどめの直前に動き出すことがある。
                // Interrupt は長い方で理由を上書きするので、中断で戻せるよう元の停止を覚えておく。
                _priorReason = _targetAgent.InterruptReason;
                _priorSeconds = _targetAgent.InterruptSecondsRemaining;
                _priorTime = Time.time;
                _targetAgent.Interrupt(move.TotalSeconds + Time.maximumDeltaTime, InterruptReasonPrefix + move.Id);
            }

            if (_targetMover != null && _targetMover.Handoff.Claim(this))
            {
                _claimedTarget = _targetMover.Handoff;
            }
        }

        private void ReleaseControls(ContextMeleeMove move, bool resumeTarget)
        {
            _claimedMover?.Release(this);
            _claimedWeapon?.Release(this);
            _claimedMover = null;
            _claimedWeapon = null;

            if (_addedModifier && _melee != null && !ReferenceEquals(_melee.Weapon, null))
            {
                _melee.Weapon.RemoveModifier(this);
            }

            _addedModifier = false;

            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            _look = null;

            if (resumeTarget && move != null && _targetAgent != null && _targetAgent.IsInitialized)
            {
                ResumeTarget(_targetAgent, move);
            }

            _claimedTarget?.Release(this);
            _claimedTarget = null;

            if (!ReferenceEquals(_self, null))
            {
                _self.Damaged -= OnSelfDamaged;
            }

            if (!ReferenceEquals(_target, null))
            {
                _target.Died -= OnTargetDied;
            }
        }

        private void ResumeTarget(EnemyAgent agent, ContextMeleeMove move)
        {
            if (!agent.Resume(InterruptReasonPrefix + move.Id))
            {
                return;
            }

            // 技の前から続いていた停止 (怯みなど) の残りは戻す。
            var remaining = _priorSeconds - (Time.time - _priorTime);
            if (!string.IsNullOrEmpty(_priorReason) && remaining > 0f)
            {
                agent.Interrupt(remaining, _priorReason);
            }

            _priorReason = string.Empty;
        }

        private void Align(float progress)
        {
            var mover = _melee != null ? _melee.Mover : null;
            var controller = mover != null ? mover.Controller : null;
            var desired = Vector3.Lerp(_alignFrom, _alignTo, progress);
            var delta = desired - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 1e-8f)
            {
                // CharacterController で動かし、壁や相手の体を突き抜けて合わせないようにする。
                if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
                {
                    controller.Move(delta);
                }
                else
                {
                    transform.position += delta;
                }
            }

            var yaw = Mathf.LerpAngle(_yawFrom, _yawTo, progress);
            if (_look != null && _look.IsInitialized)
            {
                _look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(_look.Yaw, yaw), 0f));
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private void Resolve(ContextMeleeMove move)
        {
            var target = _target;
            var presenter = _presenter;

            // とどめの Died を「技の外で倒れた」と取り違えないよう、購読を外してから当てる。
            ReleaseControls(move, false);
            ClearTarget();

            var damage = 0;
            if (target != null && !target.IsDead)
            {
                damage = move.Outcome == ContextMeleeOutcome.Kill ? target.Health.Current : move.Damage;
                var direction = target.transform.position - transform.position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : transform.forward;
                var hit = new MeleeStrike.Result(target, target.AimPoint, Vector3.Distance(transform.position, target.AimPoint), direction);
                var weapon = _melee.Weapon != null ? _melee.Weapon.Weapon : null;
                MeleeStrike.Apply(gameObject, hit, damage, weapon != null ? weapon.Spec.Id : string.Empty);
            }

            var killed = target != null && target.IsDead;
            Debug.Log(ContextMeleeLog.Resolved(AttackerId, target != null ? target.Id : string.Empty, move.Id, move.Outcome, damage, killed), this);

            // 技で倒しきれなかった (Damage の技) ときは、相手の一時停止を解いて戻す。
            if (!killed && target != null)
            {
                var agent = target.GetComponent<EnemyAgent>();
                if (agent != null && agent.IsInitialized)
                {
                    ResumeTarget(agent, move);
                }
            }

            if (killed)
            {
                GrantReward(move);
            }

            if (damage > 0 && _self != null && _self.IsLocalPlayer)
            {
                CombatEvents.RaiseHitConfirmed(new HitConfirm(damage, HitZone.Body, killed, downed: false));
            }

            presenter?.OnResolved(gameObject, target, move, damage, killed);
        }

        private void GrantReward(ContextMeleeMove move)
        {
            if (move.HealthReward <= 0 && move.AmmoReward <= 0)
            {
                return;
            }

            var healed = 0;
            if (_self != null && move.HealthReward > 0)
            {
                var health = _self.Health;
                var before = health.Current;
                health.Heal(move.HealthReward);
                healed = health.Current - before;
            }

            var ammo = 0;
            var weapon = _melee.Weapon != null ? _melee.Weapon.Weapon : null;
            if (weapon != null && move.AmmoReward > 0)
            {
                ammo = _melee.Weapon.AddReserveAmmo(weapon.Spec.Id, move.AmmoReward);
            }

            Debug.Log(ContextMeleeLog.Reward(AttackerId, move.Id, healed, ammo), this);
        }

        private void Stop(ContextMeleeInterruption reason, bool force)
        {
            if (!_run.IsActive)
            {
                return;
            }

            var move = _run.Move;
            if (!(force ? _run.Cancel() : _run.TryInterrupt()))
            {
                return;
            }

            var target = _target;
            var presenter = _presenter;
            ReleaseControls(move, true);
            ClearTarget();

            Debug.Log(ContextMeleeLog.Interrupted(AttackerId, target != null ? target.Id : string.Empty, move.Id, reason), this);
            presenter?.OnInterrupted(gameObject, target, move, reason);
        }

        private void ClearTarget()
        {
            _target = null;
            _targetAgent = null;
            _targetMover = null;
            _presenter = null;
        }

        // ほかから撃たれたとき。相手は止めてあるので、技の間に受けるダメージは相手以外からのものになる。
        private void OnSelfDamaged(Damageable self, DamageReport report)
        {
            if (!_run.IsActive)
            {
                return;
            }

            if (self.IsDead || self.IsDowned)
            {
                Stop(ContextMeleeInterruption.AttackerDown, true);
                return;
            }

            Interrupt(ContextMeleeInterruption.Shot);
        }

        private void OnTargetDied(Damageable target)
        {
            Stop(ContextMeleeInterruption.TargetLost, true);
        }

        private void SyncFinisher()
        {
            if (_melee == null)
            {
                return;
            }

            if (!_includeFinisher)
            {
                _selector.Unregister(ContextMeleeMove.FinisherId);
                _finisherSource = null;
                return;
            }

            // MeleeController.Spec は差し替えられるので、変わったときだけ作り直す (毎回作ると登録の並びが動く)。
            var spec = _melee.Spec;
            if (ReferenceEquals(spec, _finisherSource) && _selector.Find(ContextMeleeMove.FinisherId) != null)
            {
                return;
            }

            _selector.Register(ContextMeleeMove.Finisher(spec));
            _finisherSource = spec;
        }

        // 相手は MeleeStrike で探す。届く距離だけを登録した技のいちばん遠いものに広げ、前方の扇はふつうの近接と揃える。
        private MeleeSpec SearchSpec()
        {
            var reach = _selector.MaxReach();
            if (!(reach > 0f))
            {
                return null;
            }

            var spec = _melee.Spec;
            if (_searchSpec != null && ReferenceEquals(_searchSource, spec) && Mathf.Approximately(_searchReach, reach))
            {
                return _searchSpec;
            }

            _searchSpec = new MeleeSpec(spec.Damage, reach, spec.ArcDegrees, spec.SwingSeconds, spec.CooldownSeconds, spec.ImpactSeconds, spec.LungeDistance);
            _searchSource = spec;
            _searchReach = reach;
            return _searchSpec;
        }
    }
}
