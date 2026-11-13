using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン射撃と武器の持ち替え。プレイヤーのカメラに付ける。Attack を押している間、今の武器の <see cref="Weapon.TryFire"/> を呼び、
    /// 撃てたら画面中央が指す点へ <see cref="HitscanRange"/> まで Raycast して、当たったコライダーの <see cref="Hitbox"/> にダメージを渡す。
    /// 本体に <see cref="ViewRig"/> があれば始点は目で、三人称でもカメラではなくキャラの目から撃つ (<see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/>)。
    /// 無ければカメラの位置から正面へ撃つ。
    /// Next / Previous で持ち替え (<see cref="Loadout{T}"/>)、Reload で <see cref="Weapon.TryStartReload"/>、毎フレーム持ち替えと今の武器の時間を進める。
    /// 持ち替え中は撃たず、リロードも始めない。しまった武器のリロードは取り消す (<see cref="Weapon.CancelReload"/>)。残弾は武器ごとに残る。
    /// 弾数・連射間隔・リロードの判断は <see cref="Weapon"/>、ダメージ量は <see cref="DamageCalculator"/> が行う。
    /// 武器の値 (弾数・連射間隔・ダメージ・射程・距離減衰) と持ち替え秒数は <see cref="LoadoutDefinition"/> アセットから作り、このコンポーネントには持たない。
    /// 参照はコードで解決する: 照準は同じ GameObject の <see cref="Camera"/>、入力はプロジェクト全体のアクション、
    /// ロードアウトはシーン生成スクリプト (または <see cref="GameBootstrap"/>) が <see cref="LoadoutDefinition"/> に配線する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WeaponController : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = "Player/Attack";

        /// <summary>Assets/InputSystem_Actions.inputactions の Reload アクション (キーボードの R)。</summary>
        public const string ReloadActionPath = "Player/Reload";

        /// <summary>Assets/InputSystem_Actions.inputactions の Next アクション (キーボードの 2・ホイール上・パッドの十字右)。</summary>
        public const string NextActionPath = "Player/Next";

        /// <summary>Assets/InputSystem_Actions.inputactions の Previous アクション (キーボードの 1・ホイール下・パッドの十字左)。</summary>
        public const string PreviousActionPath = "Player/Previous";

        public const string ReloadedLog = "[Combat] reloaded";

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎発の確保を避ける。
        private const int MaxRaycastHits = 16;

        private const int AttackSlot = 0;
        private const int ReloadSlot = 1;
        private const int NextSlot = 2;
        private const int PreviousSlot = 3;
        private const int ActionCount = 4;

        [Header("Weapon")]
        [SerializeField, Tooltip("持つ武器の並びと持ち替え秒数。シーン生成スクリプトがデフォルトロードアウトを配線する")]
        private LoadoutDefinition _loadout;

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxRaycastHits];

        // Attack / Reload / Next / Previous の順。プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ覚えて後で無効に戻す。
        private readonly InputAction[] _actions = new InputAction[ActionCount];
        private readonly bool[] _enabledByThis = new bool[ActionCount];

        private Transform _aim;
        private ViewRig _rig;
        private Loadout<Weapon> _weapons;

        /// <summary>射撃の向きと起点。未初期化なら null。</summary>
        public Transform Aim => _aim;

        /// <summary>
        /// 持つ武器の並びと持ち替え秒数の定義。Awake (<see cref="TryInitializeFromHierarchy"/>) はこれから武器を作る。
        /// 設定しただけでは今の <see cref="Loadout"/> は変わらない。作り直すなら
        /// <see cref="Initialize(Transform, InputAction, InputAction, InputAction, InputAction, Fps.Runtime.LoadoutDefinition)"/> を呼ぶ。
        /// 実行時に AddComponent するときは、GameObject を無効にした状態で付けてここを設定してから有効にする (Awake より先に設定するため)。
        /// </summary>
        public LoadoutDefinition LoadoutDefinition
        {
            get => _loadout;
            set => _loadout = value;
        }

        /// <summary>Raycast が当たるレイヤー。</summary>
        public LayerMask HitMask
        {
            get => _hitMask;
            set => _hitMask = value;
        }

        public InputAction AttackAction => _actions[AttackSlot];
        public InputAction ReloadAction => _actions[ReloadSlot];
        public InputAction NextAction => _actions[NextSlot];
        public InputAction PreviousAction => _actions[PreviousSlot];

        /// <summary>持っている武器の並びと、今どれを持っているか。未初期化なら null。</summary>
        public Loadout<Weapon> Loadout => _weapons;

        /// <summary>今持っている武器 (持ち替え中は持ち替え先)。未初期化なら null。</summary>
        public Weapon Weapon => _weapons?.Current;

        /// <summary>今の武器の番号。未初期化なら -1。</summary>
        public int CurrentIndex => _weapons != null ? _weapons.CurrentIndex : -1;

        /// <summary>持ち替えの途中なら true。この間は撃たず、リロードも始めない。</summary>
        public bool IsSwitching => _weapons != null && _weapons.IsSwitching;

        /// <summary>
        /// 今の <see cref="Weapon"/> が別の武器になったときに、新しい Weapon を渡して呼ばれる。持ち替え・
        /// <see cref="Initialize(Transform, InputAction, InputAction, InputAction, InputAction, Fps.Runtime.LoadoutDefinition)"/>・
        /// 今の武器の <see cref="RestoreAmmo(int, int)"/> のいずれでも呼ぶ。Weapon のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Weapon> WeaponChanged;

        /// <summary>持ち替えたときに新しい番号を渡して呼ばれる。<see cref="WeaponChanged"/> の後に呼ぶ。</summary>
        public event Action<int> WeaponSwitched;

        /// <summary>今の武器の距離減衰。未初期化なら null。</summary>
        public DamageFalloff Falloff => Weapon?.Spec.Falloff;

        /// <summary>Raycast の射程 (m)。今の武器の <see cref="WeaponSpec.Range"/>。未初期化なら 0。</summary>
        public float HitscanRange => Weapon != null ? Weapon.Spec.Range : 0f;

        public bool IsInitialized
        {
            get
            {
                if (_aim == null || _weapons == null)
                {
                    return false;
                }

                foreach (var action in _actions)
                {
                    if (action == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>持ち替えたときのログ。</summary>
        public static string SwitchedLog(int index, string displayName)
        {
            return $"[Combat] switched index={index} weapon={displayName}";
        }

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
            for (var i = 0; i < ActionCount; i++)
            {
                if (_enabledByThis[i] && _actions[i] != null)
                {
                    _actions[i].Disable();
                }

                _enabledByThis[i] = false;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe(_weapons);
        }

        private void Update()
        {
            Tick(Time.deltaTime);

            // 同じフレームに両方押されたら次を優先する (打ち消し合って持ち替えの時間だけ掛かるのを避ける)。
            if (NextAction.WasPressedThisFrame())
            {
                SelectNext();
            }
            else if (PreviousAction.WasPressedThisFrame())
            {
                SelectPrevious();
            }

            if (ReloadAction.WasPressedThisFrame())
            {
                StartReload();
            }

            // 押している間は連射する。間隔は Weapon が FireInterval で制限する。
            if (AttackAction.IsPressed())
            {
                Fire();
            }
        }

        /// <summary>
        /// 同じ GameObject のカメラとプロジェクト全体の Attack / Reload / Next / Previous アクションを探し、
        /// <see cref="LoadoutDefinition"/> の武器で初期化する。
        /// 見つからない・定義が未設定か不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
            if (_loadout == null)
            {
                Debug.LogError(
                    $"[WeaponController] '{name}' に LoadoutDefinition が設定されていません。シーン生成スクリプトで配線してください。",
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

            if (!TryFindAction(actions, AttackActionPath, out var attack)
                || !TryFindAction(actions, ReloadActionPath, out var reload)
                || !TryFindAction(actions, NextActionPath, out var next)
                || !TryFindAction(actions, PreviousActionPath, out var previous))
            {
                return false;
            }

            try
            {
                Initialize(camera.transform, attack, reload, next, previous, _loadout);
            }
            catch (WeaponDefinitionException e)
            {
                // アセットの値が不正な場合 (スロットの種別に合わない武器・falloff の終わりが始まりより手前など)。
                Debug.LogError($"[WeaponController] '{name}' のロードアウトが不正です: {e.Message}", _loadout);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡し、<see cref="LoadoutDefinition"/> の武器で初期化する。定義が未設定なら <see cref="InvalidOperationException"/>、
        /// 不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(
            Transform aim, InputAction attackAction, InputAction reloadAction, InputAction nextAction, InputAction previousAction)
        {
            if (_loadout == null)
            {
                throw new InvalidOperationException(
                    $"WeaponController '{name}' has no LoadoutDefinition; set LoadoutDefinition or pass one to Initialize");
            }

            Initialize(aim, attackAction, reloadAction, nextAction, previousAction, _loadout);
        }

        /// <summary>
        /// 参照とロードアウトを直接渡して初期化する。武器は loadout.<see cref="Fps.Runtime.LoadoutDefinition.ToSpecs"/> から作り直し、
        /// 0 番の武器を持った状態で始める。成功したら <see cref="LoadoutDefinition"/> も loadout にし、<see cref="WeaponChanged"/> を呼ぶ。
        /// ロードアウトが不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(
            Transform aim,
            InputAction attackAction,
            InputAction reloadAction,
            InputAction nextAction,
            InputAction previousAction,
            LoadoutDefinition loadout)
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

            if (nextAction == null)
            {
                throw new ArgumentNullException(nameof(nextAction));
            }

            if (previousAction == null)
            {
                throw new ArgumentNullException(nameof(previousAction));
            }

            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            // 先に検査を通してから差し替える (失敗しても前の状態が残る)。
            var specs = loadout.ToSpecs();
            var weapons = new List<Weapon>(specs.Count);
            foreach (var spec in specs)
            {
                weapons.Add(new Weapon(spec));
            }

            var created = new Loadout<Weapon>(weapons, loadout.SwitchSeconds, Holster);

            var replacements = new InputAction[ActionCount];
            replacements[AttackSlot] = attackAction;
            replacements[ReloadSlot] = reloadAction;
            replacements[NextSlot] = nextAction;
            replacements[PreviousSlot] = previousAction;
            for (var i = 0; i < ActionCount; i++)
            {
                if (_enabledByThis[i] && _actions[i] != null && _actions[i] != replacements[i])
                {
                    _actions[i].Disable();
                    _enabledByThis[i] = false;
                }

                _actions[i] = replacements[i];
            }

            _aim = aim;
            _rig = aim.GetComponentInParent<ViewRig>(true);
            _loadout = loadout;
            ReplaceLoadout(created);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                EnableActions();
            }

            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>
        /// 今の武器の弾数を指定して作り直す。<see cref="RestoreAmmo(int, int, int)"/> を今の番号で呼ぶ。
        /// </summary>
        public void RestoreAmmo(int ammoInMagazine, int reserveAmmo)
        {
            EnsureInitialized();
            RestoreAmmo(_weapons.CurrentIndex, ammoInMagazine, reserveAmmo);
        }

        /// <summary>
        /// index 番の武器を、弾数を指定して作り直す。セーブデータから戻すときに使う。作り直した武器はリロード中でなくなる。
        /// 弾倉の容量を超える分は容量に揃える。持っている番号と他の武器の弾数は変えず、持ち替えの途中なら持ち替えを済ませる。
        /// 今の武器を作り直したときは <see cref="WeaponChanged"/> を呼ぶので HUD は購読し直す。
        /// </summary>
        /// <param name="index">作り直す武器の番号。0 以上 <see cref="Loadout"/> の数未満。</param>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
        public void RestoreAmmo(int index, int ammoInMagazine, int reserveAmmo)
        {
            EnsureInitialized();

            if (index < 0 || index >= _weapons.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be in [0, {_weapons.Count})");
            }

            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            var spec = _weapons.Items[index].Spec;
            var weapons = new List<Weapon>(_weapons.Items);
            weapons[index] = new Weapon(spec, Math.Min(ammoInMagazine, spec.MagazineSize), reserveAmmo);

            // Loadout の並びは作った後に変えられないので、番号を保って作り直す。
            var current = _weapons.CurrentIndex;
            ReplaceLoadout(new Loadout<Weapon>(weapons, _weapons.SwitchSeconds, Holster, current));

            if (index == current)
            {
                WeaponChanged?.Invoke(Weapon);
            }
        }

        /// <summary>
        /// index 番の武器に持ち替える。範囲外か今の番号と同じなら何もせず false。
        /// 持ち替えたら、しまった武器のリロードを取り消し、<c>[Combat] switched index=.. weapon=..</c> をログに出して
        /// <see cref="WeaponChanged"/>・<see cref="WeaponSwitched"/> を呼ぶ。
        /// </summary>
        public bool Select(int index)
        {
            EnsureInitialized();
            return _weapons.TrySelect(index);
        }

        /// <summary>次の番号の武器に持ち替える (最後の次は 0 番)。1 本しかなければ false。</summary>
        public bool SelectNext()
        {
            EnsureInitialized();
            return _weapons.SelectNext();
        }

        /// <summary>前の番号の武器に持ち替える (0 番の前は最後)。1 本しかなければ false。</summary>
        public bool SelectPrevious()
        {
            EnsureInitialized();
            return _weapons.SelectPrevious();
        }

        /// <summary>
        /// 1 発撃つ。持ち替え中か <see cref="Weapon.TryFire"/> が false なら何もせず false。
        /// 撃てたら照準の正面へ今の武器の射程まで Raycast し、Hitbox に当たれば今の武器のダメージを渡す。
        /// </summary>
        public bool Fire()
        {
            EnsureInitialized();

            var weapon = Weapon;
            if (_weapons.IsSwitching || !weapon.TryFire())
            {
                return false;
            }

            Debug.Log($"[Combat] fire ammo={weapon.AmmoInMagazine}", this);

            ResolveShot(out var origin, out var direction);
            if (TryRaycast(origin, direction, weapon.Spec.Range, out var hit))
            {
                TryApplyHit(hit.collider, hit.distance, weapon.Spec.Damage, weapon.Spec.Falloff, this, origin);
            }

            return true;
        }

        /// <summary>
        /// 今の武器の <see cref="Weapon.TryStartReload"/> を呼ぶ。持ち替え中は始めずに false。補充は <see cref="Tick"/> で時間が経ったときに行う。
        /// </summary>
        public bool StartReload()
        {
            EnsureInitialized();
            return !_weapons.IsSwitching && Weapon.TryStartReload();
        }

        /// <summary>持ち替えと今の武器の時間を deltaSeconds 進める。しまっている武器の時間は進めない。</summary>
        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            _weapons.Tick(deltaSeconds);
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

        // origin から direction へ range まで撃ち、自分の体 (照準と同じルートの階層) を除いた最も近い命中を返す。
        private bool TryRaycast(Vector3 origin, Vector3 direction, float range, out RaycastHit nearest)
        {
            var ray = new Ray(origin, direction);
            var count = Physics.RaycastNonAlloc(ray, _hitBuffer, range, _hitMask, QueryTriggerInteraction.Ignore);
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

        private bool TryFindAction(InputActionAsset actions, string path, out InputAction action)
        {
            action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogError($"[WeaponController] Input Actions '{actions.name}' に '{path}' がありません。", this);
                return false;
            }

            return true;
        }

        private void ReplaceLoadout(Loadout<Weapon> loadout)
        {
            Unsubscribe(_weapons);
            _weapons = loadout;
            _weapons.Switched += OnSwitched;
            foreach (var weapon in _weapons.Items)
            {
                weapon.Reloaded += OnReloaded;
            }
        }

        private void Unsubscribe(Loadout<Weapon> loadout)
        {
            if (loadout == null)
            {
                return;
            }

            loadout.Switched -= OnSwitched;
            foreach (var weapon in loadout.Items)
            {
                weapon.Reloaded -= OnReloaded;
            }
        }

        private static void Holster(Weapon weapon)
        {
            weapon.CancelReload();
        }

        private void OnSwitched(int index)
        {
            var weapon = _weapons.Current;
            Debug.Log(SwitchedLog(index, weapon.Spec.DisplayName), this);
            WeaponChanged?.Invoke(weapon);
            WeaponSwitched?.Invoke(index);
        }

        private void OnReloaded()
        {
            Debug.Log(ReloadedLog, this);
        }

        private void EnableActions()
        {
            for (var i = 0; i < ActionCount; i++)
            {
                if (_actions[i] != null && !_actions[i].enabled)
                {
                    _actions[i].Enable();
                    _enabledByThis[i] = true;
                }
            }
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
