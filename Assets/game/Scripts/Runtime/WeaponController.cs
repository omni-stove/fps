using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 武器の並び (Loadout) は空にできないので、武器を持っていない (IsUnarmed) 間も最後の並びを内に持ったままにし、外からは見せない。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class WeaponController : MonoBehaviour, IWeaponHolder, IPlayerCarryLoad
    {
        public const string DisarmedLog = "[Combat] disarmed";

        public const string ReloadedLog = "[Combat] reloaded";

        public const string DryFireLogPrefix = "[Combat] dry-fire";

        public const string ActiveReloadLogPrefix = "[Combat] active-reload";

        public const string SwitchedLogPrefix = "[Combat] switched";

        [Header("Weapon")]
        [SerializeField, Tooltip("持つ武器の定義の並び (既定のロードアウト)。0 番を持って始める。シーン生成スクリプトが配線する")]
        private WeaponDefinition[] _definitions = Array.Empty<WeaponDefinition>();

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly TriggerDriver _trigger = new TriggerDriver();
        private Transform _aim;
        private ViewRig _rig;
        private Loadout<Weapon> _loadout;
        private WeaponSpec[] _specs;
        private WeaponDefinition[] _loadedDefinitions;
        private GameRules _rules = GameRules.Default;
        private GameRules _loadedRules;
        private float _switchSeconds;
        private readonly RecoilState _recoil = new RecoilState();
        private readonly PelletHits<Damageable> _pelletHits = new PelletHits<Damageable>();
        private int _shotCount;
        private bool _unarmed;

        public RecoilState Recoil => _recoil;

        // 既定の 0 で足りるのは、オフラインで撃つのは手元のプレイヤーだけのため。
        public int PlayerId { get; set; }

        public int ShotCount => _shotCount;

        public Transform Aim => _aim;

        public int HitMask => _hitMask;

        // 覗き込みとロックオンは武器ではなくプレイヤーに付くので、撃つときにロックした相手を引く先を外から渡す。null ならロックしない。
        public LockOnAim LockOn { get; set; }

        public IReadOnlyList<WeaponDefinition> Definitions
        {
            get => _definitions ?? Array.Empty<WeaponDefinition>();
            set => _definitions = value != null ? ToArray(value) : Array.Empty<WeaponDefinition>();
        }

        // 既定の 0 は持ち替えを待たせない。シーンの GameBootstrap がロードアウトの秒数を入れる。
        public float SwitchSeconds
        {
            get => _switchSeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "switch seconds must be a non-negative finite number");
                }

                _switchSeconds = value;
            }
        }

        public bool IsSwitching => _loadout != null && !_unarmed && _loadout.IsSwitching;

        public GameRules Rules
        {
            get => _rules;
            set => _rules = value ?? GameRules.Default;
        }

        public GameRules LoadedRules => _loadout != null ? _loadedRules : null;

        public float SpinFraction => _trigger.Model != null ? _trigger.Model.SpinFraction : 0f;

        public float ChargeFraction => _trigger.ChargeFraction;

        public WeaponDefinition Definition => _loadout != null && !_unarmed ? _loadedDefinitions[_loadout.CurrentIndex] : null;

        public bool IsUnarmed => _unarmed;

        public IReadOnlyList<WeaponDefinition> HeldDefinitions =>
            _loadout != null && !_unarmed ? _loadedDefinitions : (IReadOnlyList<WeaponDefinition>)Array.Empty<WeaponDefinition>();

        public Loadout<Weapon> Loadout => _unarmed ? null : _loadout;

        public Weapon Weapon => _unarmed ? null : _loadout?.Current;

        public float CarrySpeedMultiplier(CarryLoadSpec carry)
        {
            return CarrySpeedMultiplier(carry, 0f);
        }

        public float CarrySpeedMultiplier(CarryLoadSpec carry, float extraWeight)
        {
            if (carry == null)
            {
                throw new ArgumentNullException(nameof(carry));
            }

            var armed = _loadout != null && !_unarmed;
            return carry.SpeedMultiplier(armed ? _loadout.Current.Spec : null, armed ? _specs : null, extraWeight);
        }

        public int CurrentIndex => _loadout != null && !_unarmed ? _loadout.CurrentIndex : -1;

        public event Action<Weapon> WeaponChanged;

        public event Action<Weapon> ReloadStarted;

        public event Action<Weapon, ActiveReloadResult> ActiveReloadJudged;

        public event Action<Weapon> Fired;

        public event Action<Weapon> DryFired;

        public IShotFilter ShotFilter { get; set; }

        public Func<bool> FireBlocker { get; set; }

        public bool IsFireBlocked => IsHolstered || IsSwitching || (FireBlocker != null && FireBlocker()) || IsFireBlockedByModifier;

        public ControlHandoff Holster { get; } = new ControlHandoff();

        public bool IsHolstered => Holster.IsHandedOff;

        public ControlHandoff Handoff { get; set; }

        public IHandedOffTrigger HandedOffTrigger { get; set; }

        public bool IsInputHandedOff => Handoff != null && Handoff.IsHandedOff;

        public DamageFalloff Falloff => Weapon?.Spec.Falloff;

        public float HitscanRange => Weapon != null ? Weapon.Spec.Range : 0f;

        public bool IsInitialized => _aim != null && _attackAction != null && _reloadAction != null && _loadout != null;

        private void Awake()
        {
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void OnDestroy()
        {
            SetLoadout(null, null, null, null);
        }

        private void Update()
        {
            if (_unarmed || IsInputHandedOff)
            {
                Tick(Time.deltaTime);
                if (!_unarmed && HandedOffTrigger != null
                    && HandedOffTrigger.TryReadTrigger(out var isPressed, out var wasPressed, out var reloadPressed))
                {
                    if (reloadPressed)
                    {
                        PressReload();
                    }

                    PullTrigger(isPressed, wasPressed);
                    return;
                }

                // 乗り物やテイクダウンは武器をしまわず入力だけ手放すので、押したままの溜めが戻った時に離した扱いで撃たれないよう捨てる。
                _trigger.Drop();
                return;
            }

            // 持ち替えを先に見るのは、同じフレームの射撃とリロードを持ち替え先の武器で行うため。
            if (_nextAction != null && _nextAction.WasPressedThisFrame())
            {
                SelectNext();
            }
            else if (_previousAction != null && _previousAction.WasPressedThisFrame())
            {
                SelectPrevious();
            }

            Tick(Time.deltaTime);

            // リロードより先に見るのは、同じフレームに両方押したとき、切り替えた種類を込めるため。
            ReadSwitchAmmoType();

            if (_reloadAction.WasPressedThisFrame())
            {
                PressReload();
            }

            PullTrigger(_attackAction.IsPressed(), _attackAction.WasPressedThisFrame());
        }

        public bool PullTrigger(bool isPressed, bool wasPressedThisFrame)
        {
            EnsureInitialized();
            var weapon = Weapon;
            _trigger.Bind(weapon?.Trigger);
            if (weapon == null)
            {
                return false;
            }

            if (IsFireBlocked || !weapon.Ammo.CanFire)
            {
                _trigger.Drop();
                if (wasPressedThisFrame && !IsFireBlocked && IsDryFire(weapon))
                {
                    Debug.Log($"{DryFireLogPrefix} id={weapon.Spec.Id}", this);
                    DryFired?.Invoke(weapon);
                    ReloadIfAuto(weapon);
                }

                return false;
            }

            var shots = _trigger.Pull(weapon.Spec.FireMode, isPressed, wasPressedThisFrame);
            var charge = _trigger.ShotChargeFraction;
            var fired = false;
            for (var i = 0; i < shots && FireShot(weapon, charge); i++)
            {
                fired = true;
            }

            if (fired)
            {
                ReloadIfAuto(weapon);
            }

            return fired;
        }

        private void ReloadIfAuto(Weapon weapon)
        {
            if (Rules.ReloadMode == ReloadMode.Auto && IsDryFire(weapon))
            {
                StartReload();
            }
        }

        public static bool IsDryFire(Weapon weapon)
        {
            return weapon != null && !weapon.IsReloading && weapon.AmmoInMagazine == 0;
        }

        public bool TryInitializeFromHierarchy()
        {
            if (_definitions == null || _definitions.Length == 0)
            {
                Debug.LogError(
                    $"[WeaponController] '{name}' に WeaponDefinition が設定されていません。シーン生成スクリプトで Definitions を配線してください。",
                    this);
                return false;
            }

            var camera = GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError($"[WeaponController] '{name}' に Camera がありません。プレイヤーのカメラに付けてください。", this);
                return false;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    "[WeaponController] プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var attack = actions.FindAction(AttackActionPath);
            if (attack == null)
            {
                Debug.LogError($"[WeaponController] Input Actions '{actions.name}' に '{AttackActionPath}' がありません。", this);
                return false;
            }

            var reload = actions.FindAction(ReloadActionPath);
            if (reload == null)
            {
                Debug.LogError($"[WeaponController] Input Actions '{actions.name}' に '{ReloadActionPath}' がありません。", this);
                return false;
            }

            var next = FindOptionalAction(actions, NextActionPath, "次の武器への持ち替え");
            var previous = FindOptionalAction(actions, PreviousActionPath, "前の武器への持ち替え");
            var switchAmmoType = FindOptionalAction(actions, SwitchAmmoTypeActionPath, "弾薬の種類の切り替え");

            try
            {
                Initialize(camera.transform, attack, reload, next, previous, _definitions);
            }
            catch (WeaponDefinitionException e)
            {
                Debug.LogError($"[WeaponController] '{name}' の武器定義が不正です: {e.Message}", this);
                return false;
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[WeaponController] '{name}' のロードアウトが不正です: {e.Message}", this);
                return false;
            }

            SetSwitchAmmoTypeAction(switchAmmoType);
            return true;
        }

        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction)
        {
            if (_definitions == null || _definitions.Length == 0)
            {
                throw new InvalidOperationException(
                    $"WeaponController '{name}' has no WeaponDefinition; set Definitions or pass them to Initialize");
            }

            Initialize(aim, attackAction, reloadAction, null, null, _definitions);
        }

        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction, WeaponDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Initialize(aim, attackAction, reloadAction, null, null, new[] { definition });
        }

        public void Initialize(
            Transform aim,
            InputAction attackAction,
            InputAction reloadAction,
            InputAction nextAction,
            InputAction previousAction,
            IReadOnlyList<WeaponDefinition> definitions)
        {
            if (aim == null)
            {
                throw new ArgumentNullException(nameof(aim));
            }

            if (attackAction == null)
            {
                throw new ArgumentNullException(nameof(attackAction));
            }

            if (reloadAction == null)
            {
                throw new ArgumentNullException(nameof(reloadAction));
            }

            // 先に検査を通してから差し替える (失敗しても前の状態が残る)。
            var loaded = ToArray(definitions ?? throw new ArgumentNullException(nameof(definitions)));
            var specs = ToSpecs(loaded);
            var rules = _rules;
            var loadout = WeaponLoadout.Create(specs, rules, _switchSeconds);

            ReplaceAction(ref _attackAction, ref _enabledAttackAction, attackAction);
            ReplaceAction(ref _reloadAction, ref _enabledReloadAction, reloadAction);
            ReplaceAction(ref _nextAction, ref _enabledNextAction, nextAction);
            ReplaceAction(ref _previousAction, ref _enabledPreviousAction, previousAction);

            _aim = aim;
            _rig = aim.GetComponentInParent<ViewRig>(true);
            _definitions = loaded;
            _unarmed = false;
            ReleaseAttachments();
            SetLoadout(loadout, specs, loaded, rules, resetAmmo: true);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                EnableActions();
            }

            WeaponChanged?.Invoke(Weapon);
        }

        public bool Fire()
        {
            EnsureInitialized();
            var weapon = Weapon;
            return FireShot(weapon, weapon != null && weapon.Trigger == _trigger.Model ? _trigger.ChargeFraction : 0f);
        }

        private bool FireShot(Weapon weapon, float chargeFraction)
        {
            if (weapon == null || IsFireBlocked || !weapon.TryFire())
            {
                return false;
            }

            Debug.Log($"[Combat] fire ammo={weapon.AmmoInMagazine}", this);
            KickSpread();
            var shotIndex = _shotCount;
            _shotCount = unchecked(_shotCount + 1);
            Fired?.Invoke(weapon);

            // この弾は今の視点の向きで撃ち、反動はカメラが次に向きを書くとき (FirstPersonLook.LateUpdate) から効く。
            ResolveShot(out var aimOrigin, out var aimDirection);
            var round = weapon.AmmoTypes.Round;
            var plan = ShotPlanner.Plan(
                round, aimOrigin.ToNumerics(), aimDirection.ToNumerics(), CurrentConeDegrees(weapon.Spec), PlayerId, shotIndex);
            _recoil.Kick(weapon.Spec, PlayerId, shotIndex);

            var rounds = new PlannedRound[plan.Rounds.Count];
            for (var i = 0; i < rounds.Length; i++)
            {
                var planned = plan.Rounds[i];
                var origin = planned.Origin.ToUnity();
                var direction = planned.Direction.ToUnity();
                ShotFilter?.Filter(ref origin, ref direction);
                rounds[i] = new PlannedRound(origin.ToNumerics(), direction.ToNumerics(), planned.Index);
            }

            var shooter = _aim.root.gameObject;
            StealthNoise.Emit(shooter, new SoundStimulus(SoundCueKind.Gunshot, rounds[0].Origin, weapon.Spec.ShotNoiseRadius, false));
            var impact = weapon.AmmoTypes.Impact;
            var charge = Mathf.Clamp01(chargeFraction);
            var delivery = ShotDeliveries.For(round.Delivery);
            var definition = Definition;
            var look = definition != null ? definition.Projectile : null;
            _pelletHits.Clear();
            var homingTarget = round.Homing != null && LockOn != null ? LockOn.FollowLocked() : null;
            var homingDecoy = homingTarget != null ? LockOn.DecoyOfLocked() : null;
            foreach (var planned in rounds)
            {
                // 撃たれた側が撃った方向へ向かえるよう、弾ごとの始点を撃った者の位置として渡す。
                var request = new ShotRequest(
                    shooter,
                    _aim.root,
                    planned.Origin.ToUnity(),
                    round,
                    impact,
                    weapon.Spec.Id,
                    _hitMask,
                    weapon.Spec.Trigger.Charge,
                    charge,
                    look,
                    ConfirmHit,
                    homingTarget,
                    homingDecoy);
                CollectHit(delivery.Deliver(request, planned));
            }

            // ペレットごとに出すと 1 回の射撃でヒットマーカーが何度も出るので、相手ごとにまとめてから出す。
            for (var i = 0; i < _pelletHits.Count; i++)
            {
                CombatEvents.RaiseHitConfirmed(_pelletHits[i].ToHitConfirm());
            }

            _pelletHits.Clear();
            return true;
        }

        // オフラインで WeaponController を持つのは手元のプレイヤーだけなので、ここが撃った本人のクライアントになる。
        private void CollectHit(ShotOutcome outcome)
        {
            if (outcome.Hitbox == null || outcome.Owner == null)
            {
                return;
            }

            Debug.Log($"[Combat] hit zone={outcome.Hitbox.Zone} dmg={outcome.Damage}", this);
            if (outcome.WasAlive)
            {
                _pelletHits.Add(
                    outcome.Owner,
                    outcome.Damage,
                    outcome.Hitbox.Zone,
                    outcome.Killed ? DamageOutcome.Killed : DamageOutcome.Damaged,
                    outcome.RaycastHit.point.ToNumerics());
            }
        }

        // 飛ぶ弾は撃った後のフレームで着くので、射撃ごとにまとめる先が無く、着いた弾ごとに知らせる。
        private void ConfirmHit(ShotOutcome outcome)
        {
            if (outcome.Hitbox == null || outcome.Owner == null)
            {
                return;
            }

            Debug.Log($"[Combat] hit zone={outcome.Hitbox.Zone} dmg={outcome.Damage}", this);
            if (outcome.WasAlive)
            {
                CombatEvents.RaiseHitConfirmed(
                    new HitConfirm(outcome.Damage, outcome.Hitbox.Zone, outcome.Killed, downed: false, outcome.RaycastHit.point.ToNumerics()));
            }
        }

        public bool StartReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || IsSwitching || IsReloadBlockedByModifier || !weapon.TryStartReload())
            {
                return false;
            }

            ReloadStarted?.Invoke(weapon);
            return true;
        }

        public bool PressReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            return weapon != null && (weapon.IsReloading ? TryActiveReload() : StartReload());
        }

        public bool TryActiveReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || IsSwitching || !weapon.TryActiveReload(out var result))
            {
                return false;
            }

            Debug.Log(ActiveReloadLog(result), this);
            ActiveReloadJudged?.Invoke(weapon, result);
            return true;
        }

        public static string ActiveReloadLog(ActiveReloadResult result)
        {
            return $"{ActiveReloadLogPrefix} {(result == ActiveReloadResult.Success ? "success" : "fail")}";
        }

        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            CancelReloadBlockedByModifier();
            var weapon = Weapon;
            weapon?.Tick(deltaSeconds);
            _loadout?.Tick(deltaSeconds);

            // 持ち替えた直後の時間をしまった武器の撃ち方に数えないよう、進める前に今の武器の撃ち方へ付け替える。
            _trigger.Bind(weapon?.Trigger);
            _trigger.Advance(deltaSeconds);
            _recoil.Tick(deltaSeconds);
            RecoverSpread(deltaSeconds);
        }

        public static bool TryApplyHit(
            Collider collider,
            float distance,
            int baseDamage,
            DamageFalloff falloff,
            UnityEngine.Object context = null,
            Vector3? sourcePosition = null,
            Vector3? direction = null)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            var hitbox = Hitbox.FindFor(collider);
            if (hitbox == null)
            {
                return false;
            }

            var owner = hitbox.Owner;
            if (owner == null)
            {
                Debug.LogError($"[Combat] Hitbox '{hitbox.name}' has no Damageable owner; the hit is ignored", hitbox);
                return false;
            }

            var damage = hitbox.ApplyHit(baseDamage, distance, falloff, sourcePosition, direction);
            Debug.Log($"[Combat] hit zone={hitbox.Zone} dmg={damage}", context != null ? context : hitbox);
            return true;
        }

        public void ResolveShot(out Vector3 origin, out Vector3 direction)
        {
            EnsureInitialized();

            // ViewRig の Awake より先に撃たれても目が決まるよう、FindEye で探す (ログは出さない)。
            var eye = _rig != null ? ViewRig.FindEye(_rig.transform) : null;
            if (eye != null && _aim.parent == eye)
            {
                ViewRig.ResolveAim(_aim, eye, HitscanRange, _aim.root, _hitMask, out origin, out direction);
                return;
            }

            origin = _aim.position;
            direction = _aim.forward;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("WeaponController is not initialized");
            }
        }
    }
}
