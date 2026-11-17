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

        public const string AttackActionPath = InputActionPaths.Attack;

        public const string ReloadActionPath = InputActionPaths.Reload;

        public const string NextActionPath = "Player/Next";

        public const string PreviousActionPath = "Player/Previous";

        public const string ReloadedLog = "[Combat] reloaded";

        public const string DryFireLogPrefix = "[Combat] dry-fire";

        public const string ActiveReloadLogPrefix = "[Combat] active-reload";

        public const string SwitchedLogPrefix = "[Combat] switched";

        public const float SpreadPerShot = 0.25f;

        public const float SpreadRecoveryDelaySeconds = 0.15f;

        public const float SpreadRecoveryPerSecond = 2f;

        [Header("Weapon")]
        [SerializeField, Tooltip("持つ武器の定義の並び (既定のロードアウト)。0 番を持って始める。シーン生成スクリプトが配線する")]
        private WeaponDefinition[] _definitions = Array.Empty<WeaponDefinition>();

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly TriggerDriver _trigger = new TriggerDriver();
        private Transform _aim;
        private ViewRig _rig;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _attackAction;
        [NonSerialized]
        private InputAction _reloadAction;
        [NonSerialized]
        private InputAction _nextAction;
        [NonSerialized]
        private InputAction _previousAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;
        private Loadout<Weapon> _loadout;
        private WeaponSpec[] _specs;
        private WeaponDefinition[] _loadedDefinitions;
        private GameRules _rules = GameRules.Default;
        private GameRules _loadedRules;
        private readonly RecoilState _recoil = new RecoilState();
        private readonly PelletHits<Damageable> _pelletHits = new PelletHits<Damageable>();
        private float _spread;
        private float _secondsSinceShot;
        private int _shotCount;
        private bool _unarmed;

        public RecoilState Recoil => _recoil;

        // 既定の 0 で足りるのは、オフラインで撃つのは手元のプレイヤーだけのため。
        public int PlayerId { get; set; }

        public int ShotCount => _shotCount;

        public Func<float> AdsProgressSource { get; set; }

        public Func<float> MoveFractionSource { get; set; }

        public Func<float> SpreadScaleSource { get; set; }

        public float SpreadScale
        {
            get
            {
                var scale = SpreadScaleSource != null ? SpreadScaleSource() : 1f;
                return float.IsNaN(scale) ? 1f : Mathf.Clamp01(scale);
            }
        }

        public float SpreadFraction => ModifySpreadFraction(_spread);

        public float CrosshairSpreadFraction => Mathf.Clamp01(SpreadFraction * SpreadScale);

        public float CrosshairConeDegrees
        {
            get
            {
                var weapon = Weapon;
                if (weapon == null)
                {
                    return 0f;
                }

                var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
                var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
                // HUD が毎フレーム読むので、源が NaN を返したフレームに CurrentConeDegrees の例外で HUD を止めない。
                if (float.IsNaN(ads) || float.IsNaN(move))
                {
                    return 0f;
                }

                var cone = CurrentConeDegrees(weapon.Spec) + weapon.AmmoTypes.Round.PelletSpreadDegrees;
                return Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
            }
        }

        public Transform Aim => _aim;

        public int HitMask => _hitMask;

        // 覗き込みとロックオンは武器ではなくプレイヤーに付くので、撃つときにロックした相手を引く先を外から渡す。null ならロックしない。
        public LockOnAim LockOn { get; set; }

        public IReadOnlyList<WeaponDefinition> Definitions
        {
            get => _definitions ?? Array.Empty<WeaponDefinition>();
            set => _definitions = value != null ? ToArray(value) : Array.Empty<WeaponDefinition>();
        }

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

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        public InputAction NextAction => _nextAction;

        public InputAction PreviousAction => _previousAction;

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

        public bool IsFireBlocked => IsHolstered || (FireBlocker != null && FireBlocker()) || IsFireBlockedByModifier;

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
            DisableIfEnabledHere(_attackAction, ref _enabledAttackAction);
            DisableIfEnabledHere(_reloadAction, ref _enabledReloadAction);
            DisableIfEnabledHere(_nextAction, ref _enabledNextAction);
            DisableIfEnabledHere(_previousAction, ref _enabledPreviousAction);
            DisableIfEnabledHere(_switchAmmoTypeAction, ref _enabledSwitchAmmoTypeAction);
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

        public bool SelectNext()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectNext();
        }

        public bool SelectPrevious()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectPrevious();
        }

        public bool Select(int index)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(index);
        }

        public bool Select(string weaponId)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(weaponId);
        }

        public bool Disarm()
        {
            EnsureInitialized();
            if (_unarmed)
            {
                return false;
            }

            _loadout.Current.CancelReload();
            _unarmed = true;
            Debug.Log(DisarmedLog, this);
            WeaponChanged?.Invoke(null);
            return true;
        }

        public IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId)
        {
            EnsureInitialized();
            if (_unarmed)
            {
                currentWeaponId = null;
                return Array.Empty<PickupWeaponState>();
            }

            var items = _loadout.Items;
            var list = new PickupWeaponState[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                list[i] = PickupWeaponState.Of(items[i]);
            }

            currentWeaponId = _loadout.Current.Spec.Id;
            return list;
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
            var loadout = WeaponLoadout.Create(specs, rules);

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

        public void ApplyRules(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            EnsureInitialized();
            var loadout = WeaponLoadout.Create(_specs, rules);
            _rules = rules;
            SetLoadout(loadout, _specs, _loadedDefinitions, rules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        public void RestoreLoadout(IEnumerable<WeaponAmmo> ammo, string equippedWeaponId)
        {
            EnsureInitialized();
            _unarmed = false;

            var loadout = WeaponLoadout.Restore(_specs, ammo, equippedWeaponId, _loadedRules, out var unknown);
            foreach (var id in unknown)
            {
                Debug.LogWarning($"[WeaponController] '{name}' は武器 '{id}' を持っていないため、セーブされたその残弾を捨てます。", this);
            }

            if (!string.IsNullOrEmpty(equippedWeaponId) && loadout.IndexOf(equippedWeaponId) < 0)
            {
                Debug.LogWarning(
                    $"[WeaponController] '{name}' はセーブで持っていた武器 '{equippedWeaponId}' を持っていないため、{loadout.Current.Spec.LogName} を持ちます。",
                    this);
            }

            SetLoadout(loadout, _specs, _loadedDefinitions, _loadedRules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        public int MaxWeapons => _rules.MaxWeapons;

        // 持てる数の上限 (MaxWeapons) はここでは見ない。上限で持ち替えるのは拾う側 (Pickup) が SwapWeapon で行う。
        public bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed)
            {
                var only = new[] { spec };
                var armed = WeaponLoadout.Restore(only, AmmoList(ammo), spec.Id, _loadedRules, out _);
                _unarmed = false;
                _attachments.Clear();
                RememberAttachments(spec.Id, kept);
                SetLoadout(armed, only, new[] { definition }, _loadedRules);
                Debug.Log($"[Combat] weapon added index=0 id={spec.Id} name={spec.LogName}", this);
                WeaponChanged?.Invoke(Weapon);
                return true;
            }

            if (_loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var specs = new WeaponSpec[_specs.Length + 1];
            Array.Copy(_specs, specs, _specs.Length);
            specs[_specs.Length] = spec;
            var definitions = new WeaponDefinition[_loadedDefinitions.Length + 1];
            Array.Copy(_loadedDefinitions, definitions, _loadedDefinitions.Length);
            definitions[_loadedDefinitions.Length] = definition;

            var carried = new List<WeaponAmmo>(_loadout.SnapshotAmmo());
            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, Weapon.Spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, -1);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon added index={specs.Length - 1} id={spec.Id} name={spec.LogName} attachments={WeaponAttachments.Describe(kept)}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public bool SwapWeapon(
            string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dropped = default;
            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed || _loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var index = _loadout.IndexOf(droppedWeaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            var leavingAmmo = WeaponAmmo.Leaving(leaving);
            var leavingDefinition = _loadedDefinitions[index];
            var leavingAttachments = AttachmentsOf(leavingAmmo.WeaponId);

            var specs = (WeaponSpec[])_specs.Clone();
            specs[index] = spec;
            var definitions = (WeaponDefinition[])_loadedDefinitions.Clone();
            definitions[index] = definition;

            var carried = new List<WeaponAmmo>();
            foreach (var entry in _loadout.SnapshotAmmo())
            {
                if (!string.Equals(entry.WeaponId, leavingAmmo.WeaponId, StringComparison.Ordinal))
                {
                    carried.Add(entry);
                }
            }

            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, index);
            _attachments.Remove(leavingAmmo.WeaponId);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            dropped = new HeldWeapon(leavingDefinition, leavingAmmo, leavingAttachments);
            Debug.Log(
                $"[Combat] weapon swapped index={index} id={spec.Id} name={spec.LogName} dropped={leavingAmmo.WeaponId} "
                + $"dropped-ammo={leavingAmmo.AmmoInMagazine}/{leavingAmmo.ReserveAmmo} dropped-attachments={WeaponAttachments.Describe(leavingAttachments)}",
                this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public bool DropWeapon(string weaponId, out HeldWeapon dropped)
        {
            dropped = default;
            EnsureInitialized();
            var index = _unarmed ? -1 : _loadout.IndexOf(weaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            dropped = new HeldWeapon(_loadedDefinitions[index], WeaponAmmo.Leaving(leaving), AttachmentsOf(leaving.Spec.Id));
            _attachments.Remove(leaving.Spec.Id);
            if (_loadout.Count == 1)
            {
                // 持ち物の山は持ち武器から作るので、武器を持っていない状態でも並びを残したまま Disarm で手放したことにする
                leaving.UseAmmoPool(null);
                Debug.Log($"[Combat] weapon dropped index=0 id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
                Disarm();
                return true;
            }

            var specs = new WeaponSpec[_specs.Length - 1];
            var definitions = new WeaponDefinition[_loadedDefinitions.Length - 1];
            var carried = new List<WeaponAmmo>();
            var ammo = _loadout.SnapshotAmmo();
            for (int from = 0, to = 0; from < _specs.Length; from++)
            {
                if (from == index)
                {
                    continue;
                }

                specs[to] = _specs[from];
                definitions[to] = _loadedDefinitions[from];
                carried.Add(ammo[from]);
                to++;
            }

            var equipped = index == _loadout.CurrentIndex ? null : Weapon.Spec.Id;
            var loadout = WeaponLoadout.Restore(specs, carried, equipped, _loadedRules, out _);
            for (var i = 0; i < loadout.Count; i++)
            {
                loadout.Items[i].AmmoTypes.TrySelect(_loadout.Items[i < index ? i : i + 1].AmmoTypes.Index);
            }

            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon dropped index={index} id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public Func<WeaponSpec, IAmmoPool> AmmoPools { get; private set; }

        // 持ち武器の並びを作り直したときに、予備弾の受け皿をつなぐ前に呼ぶ。持ち物が武器の場所を、受け皿に入る弾薬より先に取れるようにするため
        public event Action LoadoutReplaced;

        // 武器ごとの予備弾の受け皿 (持ち物の弾薬) を決め、今の武器と、この後に作り直す武器に使う。null で外す
        public void UseAmmoPools(Func<WeaponSpec, IAmmoPool> poolFor)
        {
            AmmoPools = poolFor;
            if (_loadout == null || _unarmed)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.UseAmmoPool(poolFor?.Invoke(weapon.Spec));
            }
        }

        // 残弾の数だけでは種類が戻るので、込めた弾が別の種類に変わらないよう作り直した武器へ選択を移す。replacedIndex の武器は入れ替えたので移さない。
        private void KeepAmmoTypes(Loadout<Weapon> rebuilt, int replacedIndex)
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                if (i != replacedIndex)
                {
                    rebuilt.Items[i].AmmoTypes.TrySelect(_loadout.Items[i].AmmoTypes.Index);
                }
            }
        }

        private static void RequireAmmoFor(WeaponSpec spec, WeaponAmmo ammo)
        {
            if (ammo != null && !string.Equals(ammo.WeaponId, spec.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"ammo is for weapon '{ammo.WeaponId}', not '{spec.Id}'", nameof(ammo));
            }
        }

        private static IEnumerable<WeaponAmmo> AmmoList(WeaponAmmo ammo)
        {
            return ammo != null ? new[] { ammo } : Array.Empty<WeaponAmmo>();
        }

        public int AddReserveAmmo(string weaponId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            EnsureInitialized();
            if (_unarmed)
            {
                return 0;
            }

            var index = _loadout.IndexOf(weaponId);
            return index >= 0 ? _loadout.Items[index].AddReserveAmmo(amount) : 0;
        }

        public IReadOnlyList<WeaponAmmo> SnapshotAmmo()
        {
            EnsureInitialized();
            return _unarmed ? Array.Empty<WeaponAmmo>() : _loadout.SnapshotAmmo();
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
            _spread = Mathf.Min(1f, _spread + SpreadPerShot);
            _secondsSinceShot = 0f;
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
                    homingTarget);
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
            if (weapon == null || IsReloadBlockedByModifier || !weapon.TryStartReload())
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
            if (weapon == null || !weapon.TryActiveReload(out var result))
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

        public Vector3 ApplySpread(WeaponSpec spec, Vector3 direction, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var cone = CurrentConeDegrees(spec);
            if (cone <= 0f)
            {
                return direction;
            }

            return SpreadPattern.Direction(direction.ToNumerics(), cone, PlayerId, shotIndex).ToUnity();
        }

        public float CurrentConeDegrees(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
            var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
            var cone = SpreadPattern.ConeDegrees(spec, ads, move) * SpreadScale * ModifierSpreadMultiplier();
            if (_modifiers.Count == 0)
            {
                return cone;
            }

            // HUD が毎フレーム読むので、写しを作らずに回す (modifier の中で足し外しはしない約束)。
            for (var i = 0; i < _modifiers.Count; i++)
            {
                cone = _modifiers[i].ModifyConeDegrees(spec, cone);
            }

            // NaN を返す modifier があっても、SpreadPattern.Direction に渡して例外にしない。
            return float.IsNaN(cone) ? 0f : Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
        }

        private float ModifySpreadFraction(float spread)
        {
            if (_modifiers.Count == 0)
            {
                return spread;
            }

            for (var i = 0; i < _modifiers.Count; i++)
            {
                spread = _modifiers[i].ModifySpreadFraction(spread);
            }

            return float.IsNaN(spread) ? 0f : Mathf.Clamp01(spread);
        }

        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            CancelReloadBlockedByModifier();
            var weapon = Weapon;
            weapon?.Tick(deltaSeconds);

            // 持ち替えた直後の時間をしまった武器の撃ち方に数えないよう、進める前に今の武器の撃ち方へ付け替える。
            _trigger.Bind(weapon?.Trigger);
            _trigger.Advance(deltaSeconds);
            _recoil.Tick(deltaSeconds);

            var waiting = Mathf.Max(0f, SpreadRecoveryDelaySeconds - _secondsSinceShot);
            var recovering = Mathf.Max(0f, deltaSeconds - waiting);
            _secondsSinceShot = Mathf.Min(SpreadRecoveryDelaySeconds, _secondsSinceShot + deltaSeconds);
            _spread = Mathf.Max(0f, _spread - (recovering * SpreadRecoveryPerSecond));
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

        private void SetLoadout(Loadout<Weapon> loadout, WeaponSpec[] specs, WeaponDefinition[] definitions, GameRules rules, bool resetAmmo = false)
        {
            if (_loadout != null)
            {
                _loadout.Switched -= OnSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Reloaded -= OnReloaded;
                    weapon.UseAmmoPool(null);
                }
            }

            _loadout = loadout;
            _specs = specs;
            _loadedDefinitions = definitions;
            _loadedRules = rules;
            LoadoutReplaced?.Invoke();
            if (_loadout == null)
            {
                return;
            }

            Dictionary<string, (IAmmoPool Pool, int Count)> reset = null;
            _loadout.Switched += OnSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.Reloaded += OnReloaded;

                // 武器を持っていない間の並びは手放した武器なので、その予備弾を持ち物に足さない
                if (AmmoPools == null || _unarmed)
                {
                    continue;
                }

                var own = weapon.ReserveAmmo;
                var pool = AmmoPools(weapon.Spec);
                if (weapon.UseAmmoPool(pool) && resetAmmo)
                {
                    reset ??= new Dictionary<string, (IAmmoPool Pool, int Count)>(StringComparer.Ordinal);
                    reset[pool.Id] = reset.TryGetValue(pool.Id, out var target) ? (pool, Math.Max(target.Count, own)) : (pool, own);
                }
            }

            // 受け皿につなぐと足りない分を足すだけなので、作り直しで予備弾が減る (難易度で減らす・セーブの値に戻す) ときは多い分を減らす
            if (reset != null)
            {
                foreach (var (pool, count) in reset.Values)
                {
                    var extra = pool.Count - count;
                    if (extra > 0)
                    {
                        pool.Take(extra);
                    }
                }
            }
        }

        private void OnSwitched(int index)
        {
            var spec = _loadout.Current.Spec;
            Debug.Log($"{SwitchedLogPrefix} index={index} id={spec.Id} name={spec.LogName}", this);
            WeaponChanged?.Invoke(Weapon);
        }

        private void OnReloaded()
        {
            Debug.Log(ReloadedLog, this);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("WeaponController is not initialized");
            }
        }

        private void EnableActions()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            _enabledAttackAction |= EnableIfNeeded(_attackAction);
            _enabledReloadAction |= EnableIfNeeded(_reloadAction);
            _enabledNextAction |= EnableIfNeeded(_nextAction);
            _enabledPreviousAction |= EnableIfNeeded(_previousAction);
            _enabledSwitchAmmoTypeAction |= EnableIfNeeded(_switchAmmoTypeAction);
        }

        private InputAction FindOptionalAction(InputActionAsset actions, string path, string effect)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning($"[WeaponController] Input Actions '{actions.name}' に '{path}' がないため、{effect}は入力からはできません。", this);
            }

            return action;
        }

        private static void ReplaceAction(ref InputAction current, ref bool enabledHere, InputAction next)
        {
            if (current != next)
            {
                DisableIfEnabledHere(current, ref enabledHere);
            }

            current = next;
        }

        private static void DisableIfEnabledHere(InputAction action, ref bool enabledHere)
        {
            if (enabledHere && action != null)
            {
                action.Disable();
            }

            enabledHere = false;
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action == null || action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private static WeaponDefinition[] ToArray(IReadOnlyList<WeaponDefinition> definitions)
        {
            var result = new WeaponDefinition[definitions.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = definitions[i];
            }

            return result;
        }

        private static WeaponSpec[] ToSpecs(WeaponDefinition[] definitions)
        {
            if (definitions.Length == 0)
            {
                throw new ArgumentException("definitions must not be empty", nameof(definitions));
            }

            var specs = new WeaponSpec[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null)
                {
                    throw new ArgumentException("definitions must not contain null (index " + i + ")", nameof(definitions));
                }

                specs[i] = definitions[i].ToSpec();
            }

            return specs;
        }
    }
}
