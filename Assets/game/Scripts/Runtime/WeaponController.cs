using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン射撃。プレイヤーのカメラに付ける。Attack を押している間 <see cref="Weapon.TryFire"/> を呼び、
    /// 撃てたら画面中央が指す点へ <see cref="HitscanRange"/> まで Raycast して、当たったコライダーの <see cref="Hitbox"/> にダメージを渡す。
    /// 本体に <see cref="ViewRig"/> があれば始点は目で、三人称でもカメラではなくキャラの目から撃つ (<see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/>)。
    /// 無ければカメラの位置から正面へ撃つ。
    /// Reload で <see cref="Weapon.TryStartReload"/>、毎フレーム <see cref="Weapon.Tick"/> を呼ぶ。
    /// 弾数・連射間隔・リロードの判断は <see cref="Weapon"/>、ダメージ量は <see cref="DamageCalculator"/> が行う。
    /// 武器の値 (弾数・連射間隔・ダメージ・射程・距離減衰) は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない。
    /// 参照はコードで解決する: 照準は同じ GameObject の <see cref="Camera"/>、入力はプロジェクト全体のアクション、
    /// 定義はシーン生成スクリプト (または <see cref="GameBootstrap"/>) が <see cref="Definition"/> に配線する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WeaponController : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = "Player/Attack";

        /// <summary>Assets/InputSystem_Actions.inputactions の Reload アクション (キーボードの R)。</summary>
        public const string ReloadActionPath = "Player/Reload";

        public const string ReloadedLog = "[Combat] reloaded";

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎発の確保を避ける。
        private const int MaxRaycastHits = 16;

        [Header("Weapon")]
        [SerializeField, Tooltip("撃つ武器の定義 (弾数・連射間隔・ダメージ・射程・距離減衰)。シーン生成スクリプトが配線する")]
        private WeaponDefinition _definition;

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxRaycastHits];
        private Transform _aim;
        private ViewRig _rig;
        private InputAction _attackAction;
        private InputAction _reloadAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private Weapon _weapon;
        private DamageFalloff _falloff;
        private float _range;

        /// <summary>射撃の向きと起点。未初期化なら null。</summary>
        public Transform Aim => _aim;

        /// <summary>
        /// 撃つ武器の定義。Awake (<see cref="TryInitializeFromHierarchy"/>) はこれから武器を作る。
        /// 設定しただけでは今の <see cref="Weapon"/> は変わらない。作り直すなら <see cref="Initialize(Transform, InputAction, InputAction, WeaponDefinition)"/> を呼ぶ。
        /// 実行時に AddComponent するときは、GameObject を無効にした状態で付けてここを設定してから有効にする (Awake より先に設定するため)。
        /// </summary>
        public WeaponDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        /// <summary>撃っている武器。未初期化なら null。</summary>
        public Weapon Weapon => _weapon;

        /// <summary>
        /// <see cref="Initialize(Transform, InputAction, InputAction, WeaponDefinition)"/> や <see cref="RestoreAmmo"/> で <see cref="Weapon"/> を作り直したときに、新しい Weapon を渡して呼ばれる。
        /// Weapon のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Weapon> WeaponChanged;

        public DamageFalloff Falloff => _falloff;

        /// <summary>Raycast の射程 (m)。武器の <see cref="WeaponSpec.Range"/>。未初期化なら 0。</summary>
        public float HitscanRange => _range;

        public bool IsInitialized => _aim != null && _attackAction != null && _reloadAction != null && _weapon != null;

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

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            _enabledAttackAction |= EnableIfNeeded(_attackAction);
            _enabledReloadAction |= EnableIfNeeded(_reloadAction);
        }

        private void OnDisable()
        {
            if (_enabledAttackAction && _attackAction != null)
            {
                _attackAction.Disable();
            }

            if (_enabledReloadAction && _reloadAction != null)
            {
                _reloadAction.Disable();
            }

            _enabledAttackAction = false;
            _enabledReloadAction = false;
        }

        private void OnDestroy()
        {
            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }
        }

        private void Update()
        {
            _weapon.Tick(Time.deltaTime);

            if (_reloadAction.WasPressedThisFrame())
            {
                StartReload();
            }

            // 押している間は連射する。間隔は Weapon が FireInterval で制限する。
            if (_attackAction.IsPressed())
            {
                Fire();
            }
        }

        /// <summary>
        /// 同じ GameObject のカメラとプロジェクト全体の Attack / Reload アクションを探し、<see cref="Definition"/> の武器で初期化する。
        /// 見つからない・定義が未設定か不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
            if (_definition == null)
            {
                Debug.LogError(
                    $"[WeaponController] '{name}' に WeaponDefinition が設定されていません。シーン生成スクリプトで配線してください。",
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

            try
            {
                Initialize(camera.transform, attack, reload, _definition);
            }
            catch (WeaponDefinitionException e)
            {
                // アセットの値が不正な場合 (falloff の終わりが始まりより手前など)。
                Debug.LogError($"[WeaponController] '{name}' の武器定義が不正です: {e.Message}", _definition);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡し、<see cref="Definition"/> の武器で初期化する。定義が未設定なら <see cref="InvalidOperationException"/>、
        /// 不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction)
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    $"WeaponController '{name}' has no WeaponDefinition; set Definition or pass one to Initialize");
            }

            Initialize(aim, attackAction, reloadAction, _definition);
        }

        /// <summary>
        /// 参照と武器の定義を直接渡して初期化する。武器と距離減衰は definition.<see cref="WeaponDefinition.ToSpec"/> から作り直し、
        /// 射程は <see cref="WeaponSpec.Range"/> にする。成功したら <see cref="Definition"/> も definition にする。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction, WeaponDefinition definition)
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

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            // 先に検査を通してから差し替える (失敗しても前の状態が残る)。
            var spec = definition.ToSpec();

            if (_enabledAttackAction && _attackAction != null && _attackAction != attackAction)
            {
                _attackAction.Disable();
                _enabledAttackAction = false;
            }

            if (_enabledReloadAction && _reloadAction != null && _reloadAction != reloadAction)
            {
                _reloadAction.Disable();
                _enabledReloadAction = false;
            }

            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }

            _aim = aim;
            _rig = aim.GetComponentInParent<ViewRig>(true);
            _attackAction = attackAction;
            _reloadAction = reloadAction;
            _definition = definition;
            _falloff = spec.Falloff;
            _range = spec.Range;
            _weapon = new Weapon(spec);
            _weapon.Reloaded += OnReloaded;

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                _enabledAttackAction |= EnableIfNeeded(_attackAction);
                _enabledReloadAction |= EnableIfNeeded(_reloadAction);
            }

            WeaponChanged?.Invoke(_weapon);
        }

        /// <summary>
        /// 弾数を指定して <see cref="Weapon"/> を作り直す。セーブデータから戻すときに使う。リロード中なら取り消す。
        /// 弾倉の容量を超える分は容量に揃える。<see cref="WeaponChanged"/> を呼ぶので HUD は購読し直す。
        /// </summary>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
        public void RestoreAmmo(int ammoInMagazine, int reserveAmmo)
        {
            EnsureInitialized();

            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            var spec = _weapon.Spec;
            var weapon = new Weapon(spec, Math.Min(ammoInMagazine, spec.MagazineSize), reserveAmmo);

            _weapon.Reloaded -= OnReloaded;
            _weapon = weapon;
            _weapon.Reloaded += OnReloaded;
            WeaponChanged?.Invoke(_weapon);
        }

        /// <summary>
        /// 1 発撃つ。<see cref="Weapon.TryFire"/> が false なら何もせず false。
        /// 撃てたら照準の正面へ Raycast し、Hitbox に当たればダメージを渡す。
        /// </summary>
        public bool Fire()
        {
            EnsureInitialized();

            if (!_weapon.TryFire())
            {
                return false;
            }

            Debug.Log($"[Combat] fire ammo={_weapon.AmmoInMagazine}", this);

            ResolveShot(out var origin, out var direction);
            if (TryRaycast(origin, direction, out var hit))
            {
                TryApplyHit(hit.collider, hit.distance, _weapon.Spec.Damage, _falloff, this, origin);
            }

            return true;
        }

        /// <summary><see cref="Weapon.TryStartReload"/> を呼ぶ。補充は <see cref="Tick"/> で時間が経ったときに行う。</summary>
        public bool StartReload()
        {
            EnsureInitialized();
            return _weapon.TryStartReload();
        }

        /// <summary>武器の時間を deltaSeconds 進める。</summary>
        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            _weapon.Tick(deltaSeconds);
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
                ViewRig.ResolveAim(_aim, eye, _range, _aim.root, _hitMask, out origin, out direction);
                return;
            }

            origin = _aim.position;
            direction = _aim.forward;
        }

        // origin から direction へ射程まで撃ち、自分の体 (照準と同じルートの階層) を除いた最も近い命中を返す。
        private bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit nearest)
        {
            var ray = new Ray(origin, direction);
            var count = Physics.RaycastNonAlloc(ray, _hitBuffer, _range, _hitMask, QueryTriggerInteraction.Ignore);
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

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }
    }
}
