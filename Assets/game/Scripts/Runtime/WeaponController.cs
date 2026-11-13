using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン射撃。プレイヤーのカメラに付ける。Attack を武器の射撃モードに従って (Auto は押している間、Semi は押すたびに) <see cref="Weapon.TryFire"/> を呼び、
    /// 撃てたら画面中央が指す点へ <see cref="HitscanRange"/> まで Raycast して、当たったコライダーの <see cref="Hitbox"/> にダメージを渡す。
    /// 本体に <see cref="ViewRig"/> があれば始点は目で、三人称でもカメラではなくキャラの目から撃つ (<see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/>)。
    /// 無ければカメラの位置から正面へ撃つ。
    /// 武器は <see cref="Definitions"/> (既定のロードアウト) の並びで 1 本ずつ持ち (<see cref="Loadout"/>)、Next / Previous で持ち替える (端で折り返す)。
    /// 持ち替えに時間はかからず、しまった武器のリロードは取り消す。
    /// Reload で <see cref="Weapon.TryStartReload"/>、毎フレーム今の武器の <see cref="Weapon.Tick"/> を呼ぶ。
    /// 弾数・連射間隔・リロードの判断は <see cref="Weapon"/>、ダメージ量は <see cref="DamageCalculator"/> が行う。
    /// 武器の値 (弾数・連射間隔・ダメージ・射程・距離減衰) は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない。
    /// 参照はコードで解決する: 照準は同じ GameObject の <see cref="Camera"/>、入力はプロジェクト全体のアクション、
    /// 定義はシーン生成スクリプト (または <see cref="GameBootstrap"/>) が <see cref="Definitions"/> に配線する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WeaponController : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = InputActionPaths.Attack;

        /// <summary>Assets/InputSystem_Actions.inputactions の Reload アクション (キーボードの R、ゲームパッドの X)。</summary>
        public const string ReloadActionPath = InputActionPaths.Reload;

        /// <summary>Assets/InputSystem_Actions.inputactions の Next アクション。次の番号の武器に持ち替える。</summary>
        public const string NextActionPath = "Player/Next";

        /// <summary>Assets/InputSystem_Actions.inputactions の Previous アクション。前の番号の武器に持ち替える。</summary>
        public const string PreviousActionPath = "Player/Previous";

        public const string ReloadedLog = "[Combat] reloaded";

        /// <summary>持ち替えたときのログの頭。続けて <c>index=.. id=.. name=..</c> を出す。</summary>
        public const string SwitchedLogPrefix = "[Combat] switched";

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎発の確保を避ける。
        private const int MaxRaycastHits = 16;

        [Header("Weapon")]
        [SerializeField, Tooltip("持つ武器の定義の並び (既定のロードアウト)。0 番を持って始める。シーン生成スクリプトが配線する")]
        private WeaponDefinition[] _definitions = Array.Empty<WeaponDefinition>();

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxRaycastHits];
        private Transform _aim;
        private ViewRig _rig;
        private InputAction _attackAction;
        private InputAction _reloadAction;
        private InputAction _nextAction;
        private InputAction _previousAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;
        private Loadout<Weapon> _loadout;
        private WeaponSpec[] _specs;
        private WeaponDefinition[] _loadedDefinitions;

        /// <summary>射撃の向きと起点。未初期化なら null。</summary>
        public Transform Aim => _aim;

        /// <summary>
        /// 持つ武器の定義の並び。Awake (<see cref="TryInitializeFromHierarchy"/>) はこれから武器を作る。
        /// 設定しただけでは今の <see cref="Loadout"/> は変わらない。作り直すなら <see cref="Initialize(Transform, InputAction, InputAction)"/> などを呼ぶ。
        /// 実行時に AddComponent するときは、GameObject を無効にした状態で付けてここを設定してから有効にする (Awake より先に設定するため)。
        /// null を設定すると空になる。
        /// </summary>
        public IReadOnlyList<WeaponDefinition> Definitions
        {
            get => _definitions ?? Array.Empty<WeaponDefinition>();
            set => _definitions = value != null ? ToArray(value) : Array.Empty<WeaponDefinition>();
        }

        /// <summary>今持っている武器の定義。未初期化なら null。</summary>
        public WeaponDefinition Definition => _loadout != null ? _loadedDefinitions[_loadout.CurrentIndex] : null;

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        /// <summary>次の武器に持ち替えるアクション。渡されていなければ null (入力では持ち替えない)。</summary>
        public InputAction NextAction => _nextAction;

        /// <summary>前の武器に持ち替えるアクション。渡されていなければ null (入力では持ち替えない)。</summary>
        public InputAction PreviousAction => _previousAction;

        /// <summary>持っている武器の並び。未初期化なら null。</summary>
        public Loadout<Weapon> Loadout => _loadout;

        /// <summary>今持っている武器。未初期化なら null。</summary>
        public Weapon Weapon => _loadout?.Current;

        /// <summary>今持っている武器の番号。未初期化なら -1。</summary>
        public int CurrentIndex => _loadout != null ? _loadout.CurrentIndex : -1;

        /// <summary>
        /// 今持っている <see cref="Weapon"/> が変わったとき (初期化・持ち替え・<see cref="RestoreLoadout"/>) に、新しい Weapon を渡して呼ばれる。
        /// Weapon のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Weapon> WeaponChanged;

        /// <summary><see cref="StartReload"/> でリロードが始まったときに、リロードする Weapon を渡して呼ばれる。</summary>
        public event Action<Weapon> ReloadStarted;

        /// <summary>今持っている武器の距離減衰。未初期化なら null。</summary>
        public DamageFalloff Falloff => Weapon?.Spec.Falloff;

        /// <summary>Raycast の射程 (m)。今持っている武器の <see cref="WeaponSpec.Range"/>。未初期化なら 0。</summary>
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
        }

        private void OnDestroy()
        {
            SetLoadout(null, null, null);
        }

        private void Update()
        {
            // 持ち替えを先に見るのは、同じフレームの射撃とリロードを持ち替え先の武器で行うため。
            if (_nextAction != null && _nextAction.WasPressedThisFrame())
            {
                SelectNext();
            }
            else if (_previousAction != null && _previousAction.WasPressedThisFrame())
            {
                SelectPrevious();
            }

            Weapon.Tick(Time.deltaTime);

            if (_reloadAction.WasPressedThisFrame())
            {
                StartReload();
            }

            PullTrigger(_attackAction.IsPressed(), _attackAction.WasPressedThisFrame());
        }

        /// <summary>
        /// Attack の入力を今の武器の <see cref="WeaponSpec.FireMode"/> で判断し (<see cref="FireTrigger.Wants"/>)、撃つなら <see cref="Fire"/> する。
        /// Auto は押している間、Semi は押したフレームだけ撃つ。連射間隔は <see cref="Weapon"/> が制限する。撃てたら true。
        /// </summary>
        /// <param name="isPressed">Attack が押されているか。</param>
        /// <param name="wasPressedThisFrame">Attack がこのフレームに押されたか。</param>
        public bool PullTrigger(bool isPressed, bool wasPressedThisFrame)
        {
            EnsureInitialized();
            return FireTrigger.Wants(Weapon.Spec.FireMode, isPressed, wasPressedThisFrame) && Fire();
        }

        /// <summary>次の番号の武器に持ち替える (最後の次は 0 番)。1 本しかなければ false。</summary>
        public bool SelectNext()
        {
            EnsureInitialized();
            return _loadout.SelectNext();
        }

        /// <summary>前の番号の武器に持ち替える (0 番の前は最後)。1 本しかなければ false。</summary>
        public bool SelectPrevious()
        {
            EnsureInitialized();
            return _loadout.SelectPrevious();
        }

        /// <summary>index 番の武器に持ち替える。範囲外か今の番号なら false。</summary>
        public bool Select(int index)
        {
            EnsureInitialized();
            return _loadout.TrySelect(index);
        }

        /// <summary>Id が weaponId の武器に持ち替える。持っていないか今の武器なら false。</summary>
        public bool Select(string weaponId)
        {
            EnsureInitialized();
            return _loadout.TrySelect(weaponId);
        }

        /// <summary>
        /// 同じ GameObject のカメラとプロジェクト全体の Attack / Reload / Next / Previous アクションを探し、<see cref="Definitions"/> の武器で初期化する。
        /// Next / Previous が無ければ警告を出し、持ち替えの入力だけを無効にする。
        /// それ以外が見つからない・定義が未設定か不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
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

            try
            {
                Initialize(camera.transform, attack, reload, next, previous, _definitions);
            }
            catch (WeaponDefinitionException e)
            {
                // アセットの値が不正な場合 (falloff の終わりが始まりより手前など)。
                Debug.LogError($"[WeaponController] '{name}' の武器定義が不正です: {e.Message}", this);
                return false;
            }
            catch (ArgumentException e)
            {
                // 並びに null や同じ Id の武器が入っている場合。
                Debug.LogError($"[WeaponController] '{name}' のロードアウトが不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡し、<see cref="Definitions"/> の武器で初期化する。持ち替えの入力は無い (<see cref="SelectNext"/> などで持ち替える)。
        /// 定義が未設定なら <see cref="InvalidOperationException"/>、不正なら <see cref="WeaponDefinitionException"/> か
        /// <see cref="ArgumentException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction)
        {
            if (_definitions == null || _definitions.Length == 0)
            {
                throw new InvalidOperationException(
                    $"WeaponController '{name}' has no WeaponDefinition; set Definitions or pass them to Initialize");
            }

            Initialize(aim, attackAction, reloadAction, null, null, _definitions);
        }

        /// <summary>武器 1 本 (definition) だけを持たせて初期化する。持ち替えの入力は無い。</summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction, WeaponDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Initialize(aim, attackAction, reloadAction, null, null, new[] { definition });
        }

        /// <summary>
        /// 参照と武器の定義の並びを直接渡して初期化する。武器は各定義の <see cref="WeaponDefinition.ToSpec"/> から満タンで作り直し、0 番を持つ。
        /// 成功したら <see cref="Definitions"/> も definitions にし、<see cref="WeaponChanged"/> を呼ぶ。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/>、並びが空・null を含む・Id が重複していれば <see cref="ArgumentException"/> を投げ、
        /// 状態は変えない。
        /// </summary>
        /// <param name="nextAction">次の武器に持ち替えるアクション。null なら入力では持ち替えない。</param>
        /// <param name="previousAction">前の武器に持ち替えるアクション。null なら入力では持ち替えない。</param>
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
            var loadout = WeaponLoadout.Create(specs);

            ReplaceAction(ref _attackAction, ref _enabledAttackAction, attackAction);
            ReplaceAction(ref _reloadAction, ref _enabledReloadAction, reloadAction);
            ReplaceAction(ref _nextAction, ref _enabledNextAction, nextAction);
            ReplaceAction(ref _previousAction, ref _enabledPreviousAction, previousAction);

            _aim = aim;
            _rig = aim.GetComponentInParent<ViewRig>(true);
            _definitions = loaded;
            SetLoadout(loadout, specs, loaded);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                EnableActions();
            }

            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>
        /// 持っている全武器の弾数と持っている武器をセーブデータから戻す。リロード中なら取り消す。
        /// Id が ammo にある武器はその弾数 (弾倉の容量を超える分は容量に揃える)、無い武器は満タンにし、equippedWeaponId の武器を持つ
        /// (持っていない Id か空なら 0 番)。ammo にあって持っていない武器は警告を出して捨てる。
        /// <see cref="WeaponChanged"/> を呼ぶので HUD は購読し直す。
        /// </summary>
        /// <param name="ammo">武器ごとの残弾 (<see cref="CampaignSave.Weapons"/>)。null を含まない。</param>
        /// <param name="equippedWeaponId">持っていた武器の Id (<see cref="CampaignSave.EquippedWeaponId"/>)。null 可。</param>
        public void RestoreLoadout(IEnumerable<WeaponAmmo> ammo, string equippedWeaponId)
        {
            EnsureInitialized();

            var loadout = WeaponLoadout.Restore(_specs, ammo, equippedWeaponId, out var unknown);
            foreach (var id in unknown)
            {
                Debug.LogWarning($"[WeaponController] '{name}' は武器 '{id}' を持っていないため、セーブされたその残弾を捨てます。", this);
            }

            if (!string.IsNullOrEmpty(equippedWeaponId) && loadout.IndexOf(equippedWeaponId) < 0)
            {
                Debug.LogWarning(
                    $"[WeaponController] '{name}' はセーブで持っていた武器 '{equippedWeaponId}' を持っていないため、{loadout.Current.Spec.DisplayName} を持ちます。",
                    this);
            }

            SetLoadout(loadout, _specs, _loadedDefinitions);
            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>持っている全武器の今の残弾。並びの順。セーブに使う。</summary>
        public IReadOnlyList<WeaponAmmo> SnapshotAmmo()
        {
            EnsureInitialized();
            return _loadout.SnapshotAmmo();
        }

        /// <summary>
        /// 今の武器で 1 発撃つ。<see cref="Weapon.TryFire"/> が false なら何もせず false。
        /// 撃てたら照準の正面へ Raycast し、Hitbox に当たればダメージを渡す。
        /// </summary>
        public bool Fire()
        {
            EnsureInitialized();

            var weapon = Weapon;
            if (!weapon.TryFire())
            {
                return false;
            }

            Debug.Log($"[Combat] fire ammo={weapon.AmmoInMagazine}", this);

            ResolveShot(out var origin, out var direction);
            if (TryRaycast(origin, direction, out var hit))
            {
                TryApplyHit(hit.collider, hit.distance, weapon.Spec.Damage, weapon.Spec.Falloff, this, origin);
            }

            return true;
        }

        /// <summary>
        /// 今の武器の <see cref="Weapon.TryStartReload"/> を呼ぶ。補充は <see cref="Tick"/> で時間が経ったときに行う。
        /// 始まったら <see cref="ReloadStarted"/> を呼んで true。
        /// </summary>
        public bool StartReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (!weapon.TryStartReload())
            {
                return false;
            }

            ReloadStarted?.Invoke(weapon);
            return true;
        }

        /// <summary>今の武器の時間を deltaSeconds 進める。</summary>
        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            Weapon.Tick(deltaSeconds);
        }

        /// <summary>
        /// 当たったコライダーから <see cref="Hitbox"/> を探し、あれば持ち主にダメージを渡して
        /// <c>[Combat] hit zone=.. dmg=..</c> をログに出す。Hitbox が無ければ何もせず false。
        /// </summary>
        /// <param name="collider">命中したコライダー。</param>
        /// <param name="distance">射手から命中点までの距離。</param>
        /// <param name="baseDamage">武器の基本ダメージ。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        /// <param name="context">ログの文脈にするオブジェクト。null 可。</param>
        /// <param name="sourcePosition">撃った者の位置。撃たれた敵がこの方向へ向かう。分からなければ null。</param>
        public static bool TryApplyHit(
            Collider collider,
            float distance,
            int baseDamage,
            DamageFalloff falloff,
            UnityEngine.Object context = null,
            Vector3? sourcePosition = null)
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

            if (hitbox.Owner == null)
            {
                Debug.LogError($"[Combat] Hitbox '{hitbox.name}' has no Damageable owner; the hit is ignored", hitbox);
                return false;
            }

            var damage = hitbox.ApplyHit(baseDamage, distance, falloff, sourcePosition);
            Debug.Log($"[Combat] hit zone={hitbox.Zone} dmg={damage}", context != null ? context : hitbox);
            return true;
        }

        /// <summary>
        /// 射撃の始点と向き。照準が <see cref="ViewRig"/> のカメラなら目から画面中央が指す点へ (一人称・三人称共通)、
        /// そうでなければ照準の位置から正面へ。
        /// </summary>
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

        // origin から direction へ射程まで撃ち、自分の体 (照準と同じルートの階層) を除いた最も近い命中を返す。
        private bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit nearest)
        {
            var ray = new Ray(origin, direction);
            var count = Physics.RaycastNonAlloc(ray, _hitBuffer, HitscanRange, _hitMask, QueryTriggerInteraction.Ignore);
            var self = _aim.root;

            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = _hitBuffer[i];
                if (candidate.collider.transform.IsChildOf(self))
                {
                    continue;
                }

                if (!found || candidate.distance < nearest.distance)
                {
                    nearest = candidate;
                    found = true;
                }
            }

            Array.Clear(_hitBuffer, 0, count);
            return found;
        }

        private void SetLoadout(Loadout<Weapon> loadout, WeaponSpec[] specs, WeaponDefinition[] definitions)
        {
            if (_loadout != null)
            {
                _loadout.Switched -= OnSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Reloaded -= OnReloaded;
                }
            }

            _loadout = loadout;
            _specs = specs;
            _loadedDefinitions = definitions;
            if (_loadout != null)
            {
                _loadout.Switched += OnSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Reloaded += OnReloaded;
                }
            }
        }

        private void OnSwitched(int index)
        {
            var spec = _loadout.Current.Spec;
            Debug.Log($"{SwitchedLogPrefix} index={index} id={spec.Id} name={spec.DisplayName}", this);
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
