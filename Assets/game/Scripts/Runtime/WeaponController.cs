using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン射撃。プレイヤーのカメラに付ける。Attack を押している間 <see cref="Weapon.TryFire"/> を呼び、
    /// 撃てたらカメラの正面へ <see cref="HitscanRange"/> まで Raycast して、当たったコライダーの <see cref="Hitbox"/> にダメージを渡す。
    /// Reload で <see cref="Weapon.TryStartReload"/>、毎フレーム <see cref="Weapon.Tick"/> を呼ぶ。
    /// 弾数・連射間隔・リロードの判断は <see cref="Weapon"/>、ダメージ量は <see cref="DamageCalculator"/> が行う。
    /// 参照はコードで解決する: 照準は同じ GameObject の <see cref="Camera"/>、入力はプロジェクト全体のアクション。
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

        public const int DefaultMagazineSize = 30;
        public const int DefaultReserveAmmo = 90;
        public const float DefaultFireInterval = 0.1f;
        public const float DefaultReloadSeconds = 2f;
        public const int DefaultDamage = 25;
        public const float DefaultRange = 100f;
        public const float DefaultFalloffStart = 20f;
        public const float DefaultFalloffEnd = 60f;
        public const float DefaultFalloffMinMultiplier = 0.5f;

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎発の確保を避ける。
        private const int MaxRaycastHits = 16;

        [Header("Weapon")]
        [SerializeField, Min(1), Tooltip("弾倉の容量")]
        private int _magazineSize = DefaultMagazineSize;

        [SerializeField, Min(0), Tooltip("予備弾の初期値")]
        private int _reserveAmmo = DefaultReserveAmmo;

        [SerializeField, Min(0f), Tooltip("射撃から次に撃てるまでの秒数")]
        private float _fireInterval = DefaultFireInterval;

        [SerializeField, Min(0f), Tooltip("リロード開始から補充までの秒数")]
        private float _reloadSeconds = DefaultReloadSeconds;

        [SerializeField, Min(0), Tooltip("1 発あたりの基本ダメージ")]
        private int _damage = DefaultDamage;

        [Header("Hitscan")]
        [SerializeField, Min(0.01f), Tooltip("射程 (m)")]
        private float _range = DefaultRange;

        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        [Header("Damage falloff")]
        [SerializeField, Min(0f), Tooltip("この距離までは減衰しない (m)")]
        private float _falloffStart = DefaultFalloffStart;

        [SerializeField, Min(0f), Tooltip("この距離以上で最小倍率になる (m)")]
        private float _falloffEnd = DefaultFalloffEnd;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("最大まで減衰したときの倍率")]
        private float _falloffMinMultiplier = DefaultFalloffMinMultiplier;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxRaycastHits];
        private Transform _aim;
        private InputAction _attackAction;
        private InputAction _reloadAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private Weapon _weapon;
        private DamageFalloff _falloff;

        /// <summary>射撃の向きと起点。未初期化なら null。</summary>
        public Transform Aim => _aim;

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        /// <summary>撃っている武器。未初期化なら null。</summary>
        public Weapon Weapon => _weapon;

        /// <summary>
        /// <see cref="Initialize"/> で <see cref="Weapon"/> を作り直したときに、新しい Weapon を渡して呼ばれる。
        /// Weapon のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Weapon> WeaponChanged;

        public DamageFalloff Falloff => _falloff;

        /// <summary>Raycast の射程 (m)。</summary>
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
        /// 同じ GameObject のカメラとプロジェクト全体の Attack / Reload アクションを探して初期化する。
        /// 見つからなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
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
                Initialize(camera.transform, attack, reload);
            }
            catch (ArgumentException e)
            {
                // Inspector で不正な値 (falloff の終わりが始まりより手前など) にされた場合。
                Debug.LogError($"[WeaponController] '{name}' の武器設定が不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡して初期化する。武器と距離減衰はこのコンポーネントの設定値で作り直す。
        /// 設定値が不正なら <see cref="ArgumentException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction)
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

            if (!(_range > 0f) || float.IsInfinity(_range))
            {
                throw new ArgumentOutOfRangeException(nameof(HitscanRange), _range, "range must be a positive finite number");
            }

            // 先に両方作って検査を通してから差し替える (途中で失敗しても前の状態が残る)。
            var spec = new WeaponSpec(_magazineSize, _reserveAmmo, _fireInterval, _reloadSeconds, _damage);
            var falloff = new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier);

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
            _attackAction = attackAction;
            _reloadAction = reloadAction;
            _falloff = falloff;
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

            if (TryRaycast(out var hit))
            {
                TryApplyHit(hit.collider, hit.distance, _weapon.Spec.Damage, _falloff, this, _aim.position);
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

        // 照準の正面へ射程まで撃ち、自分の体 (照準と同じルートの階層) を除いた最も近い命中を返す。
        private bool TryRaycast(out RaycastHit nearest)
        {
            var ray = new Ray(_aim.position, _aim.forward);
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
