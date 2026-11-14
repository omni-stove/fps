using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーの今のアクションを毎フレーム 1 つの <see cref="PlayerActionState"/> にまとめて <see cref="Current"/> で渡す。
    /// デバッグ表示 (<see cref="IHudFactory.AttachActionDebug"/> が付ける) と自機モック (<see cref="PlayerMockBody"/>) が読む。読むだけで、何も書き換えない。
    /// 読み元: <see cref="PlayerController"/>・<see cref="AimDownSights"/>・<see cref="CoverController"/>・<see cref="BlindFireController"/>・
    /// <see cref="Damageable"/>・<see cref="ViewRig"/> は同じ GameObject、<see cref="WeaponController"/> は子 (プレイヤーのカメラ)。
    /// 参照はコードで解決するので Inspector の配線は要らない (<see cref="GameBootstrap"/> は <see cref="Bind"/> で直接渡す)。
    /// 解決できないか途中で消えたら、足りないものを <see cref="LogTag"/> 付きの Debug.LogError に出して自分を無効にする。
    /// </summary>
    [DisallowMultipleComponent]
    // 他の部品が Update で状態を変え終えてから読むので LateUpdate で組み立てる。さらに ViewRig などの LateUpdate より後にする。
    [DefaultExecutionOrder(100)]
    public sealed class PlayerActionReader : MonoBehaviour
    {
        public const string LogTag = "[ActionReader]";

        private PlayerController _mover;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private CoverController _cover;
        private BlindFireController _blindFire;
        private Damageable _damageable;
        private ViewRig _rig;

        private PlayerActionState _current;
        private ActiveReloadResult? _activeReload;
        private float _secondsSinceShot = float.PositiveInfinity;
        private bool _shotSinceRefresh;

        /// <summary>最後に <see cref="Refresh"/> で組み立てた状態。まだ組み立てていなければ <c>default</c>。</summary>
        public PlayerActionState Current => _current;

        public PlayerController Mover => _mover;
        public WeaponController Weapon => _weapon;
        public AimDownSights Ads => _ads;
        public CoverController Cover => _cover;
        public BlindFireController BlindFire => _blindFire;
        public Damageable Damageable => _damageable;
        public ViewRig Rig => _rig;

        /// <summary>読み元がすべて揃っているか (途中で破棄されたものがあれば false)。</summary>
        public bool IsBound =>
            _mover != null
            && _weapon != null
            && _ads != null
            && _cover != null
            && _blindFire != null
            && _damageable != null
            && _rig != null;

        private void Awake()
        {
            if (!IsBound && !TryBindFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeWeapon();
        }

        private void LateUpdate()
        {
            if (!IsBound)
            {
                Debug.LogError($"{LogTag} '{name}' の {MissingReferences()} が破棄されたため、アクションの読み取りを止めます。", this);
                enabled = false;
                return;
            }

            Refresh(Time.deltaTime);
        }

        /// <summary>
        /// 同じ GameObject の PlayerController・AimDownSights・CoverController・BlindFireController・Damageable・ViewRig と、
        /// 子の WeaponController を探して <see cref="Bind"/> する。足りなければ Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBindFromHierarchy()
        {
            var mover = GetComponent<PlayerController>();
            var weapon = GetComponentInChildren<WeaponController>(true);
            var ads = GetComponent<AimDownSights>();
            var cover = GetComponent<CoverController>();
            var blindFire = GetComponent<BlindFireController>();
            var damageable = GetComponent<Damageable>();
            var rig = GetComponent<ViewRig>();

            var missing = Missing(mover, weapon, ads, cover, blindFire, damageable, rig);
            if (missing.Count > 0)
            {
                Debug.LogError(
                    $"{LogTag} '{name}' に {string.Join(", ", missing)} がありません。プレイヤーの本体 (WeaponController は子のカメラ) に付けてください。",
                    this);
                return false;
            }

            Bind(mover, weapon, ads, cover, blindFire, damageable, rig);
            return true;
        }

        /// <summary>
        /// 読み元を直接渡す。weapon はこの GameObject かその子、それ以外はこの GameObject のもの。
        /// 前に渡した武器の購読は外し、射撃とアクティブリロードの記録は捨てる (<see cref="Current"/> は次の <see cref="Refresh"/> まで前の値)。
        /// </summary>
        public void Bind(
            PlayerController mover,
            WeaponController weapon,
            AimDownSights ads,
            CoverController cover,
            BlindFireController blindFire,
            Damageable damageable,
            ViewRig rig)
        {
            RequireNotNull(mover, nameof(mover));
            RequireNotNull(weapon, nameof(weapon));
            RequireNotNull(ads, nameof(ads));
            RequireNotNull(cover, nameof(cover));
            RequireNotNull(blindFire, nameof(blindFire));
            RequireNotNull(damageable, nameof(damageable));
            RequireNotNull(rig, nameof(rig));

            RequireOnSelf(mover, nameof(mover));
            RequireOnSelf(ads, nameof(ads));
            RequireOnSelf(cover, nameof(cover));
            RequireOnSelf(blindFire, nameof(blindFire));
            RequireOnSelf(damageable, nameof(damageable));
            RequireOnSelf(rig, nameof(rig));
            if (!weapon.transform.IsChildOf(transform))
            {
                throw new ArgumentException($"weapon must be on '{name}' or its children", nameof(weapon));
            }

            UnsubscribeWeapon();
            _mover = mover;
            _weapon = weapon;
            _ads = ads;
            _cover = cover;
            _blindFire = blindFire;
            _damageable = damageable;
            _rig = rig;
            ResetWeaponRecord();

            _weapon.Fired += OnFired;
            _weapon.ReloadStarted += OnReloadStarted;
            _weapon.ActiveReloadJudged += OnActiveReloadJudged;
            _weapon.WeaponChanged += OnWeaponChanged;
        }

        /// <summary>
        /// 読み元から今の状態を組み立てて <see cref="Current"/> にし、それを返す。毎フレーム <see cref="LateUpdate"/> から呼ぶ。
        /// 読み元が未初期化 (Edit Mode や専用サーバー) でも、その部分を「していない」として組み立てる。
        /// </summary>
        /// <param name="deltaSeconds">前の Refresh からの秒数。射撃中かを決めるのに使う。0 以上の有限値。</param>
        public PlayerActionState Refresh(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsBound)
            {
                throw new InvalidOperationException($"PlayerActionReader '{name}' is missing {MissingReferences()}");
            }

            // 撃ったフレームの Refresh で 0 から数え始めると、そのフレームの経過を射撃の後の時間に数えずに済む。
            _secondsSinceShot = _shotSinceRefresh ? 0f : _secondsSinceShot + deltaSeconds;
            _shotSinceRefresh = false;

            var weapon = _weapon.Weapon;
            var flags = PlayerActionConditions.None;
            Set(ref flags, PlayerActionConditions.Sprinting, _mover.IsSprinting);
            Set(ref flags, PlayerActionConditions.Crouching, _mover.IsCrouched);
            Set(ref flags, PlayerActionConditions.Dodging, _mover.IsDodging);
            Set(ref flags, PlayerActionConditions.Vaulting, _mover.IsVaulting);
            Set(ref flags, PlayerActionConditions.Grounded, _mover.IsGrounded);
            Set(ref flags, PlayerActionConditions.InCover, _cover.IsInCover);
            Set(ref flags, PlayerActionConditions.BlindFiring, _blindFire.IsBlindFiring);
            Set(ref flags, PlayerActionConditions.Aiming, _ads.IsAiming);
            Set(ref flags, PlayerActionConditions.Firing, weapon != null && PlayerActionState.IsFiringAfter(_secondsSinceShot, weapon.Spec.FireInterval));
            Set(ref flags, PlayerActionConditions.Reloading, weapon != null && weapon.IsReloading);
            Set(ref flags, PlayerActionConditions.Downed, _damageable.IsDowned);

            _current = new PlayerActionState(
                flags,
                weapon != null ? weapon.ReloadProgress : 0f,
                _activeReload,
                _rig.Shoulder,
                _rig.Mode,
                weapon != null ? weapon.Spec.LogName : string.Empty,
                _blindFire.SpreadDegrees);
            return _current;
        }

        private void OnFired(Weapon _)
        {
            _shotSinceRefresh = true;
        }

        private void OnReloadStarted(Weapon _)
        {
            _activeReload = null;
        }

        private void OnActiveReloadJudged(Weapon _, ActiveReloadResult result)
        {
            _activeReload = result;
        }

        // 持ち替えで前の武器のリロードは取り消されるので、その判定も射撃中も持ち越さない。
        private void OnWeaponChanged(Weapon _)
        {
            ResetWeaponRecord();
        }

        private void ResetWeaponRecord()
        {
            _activeReload = null;
            _secondsSinceShot = float.PositiveInfinity;
            _shotSinceRefresh = false;
        }

        private void UnsubscribeWeapon()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (ReferenceEquals(_weapon, null))
            {
                return;
            }

            _weapon.Fired -= OnFired;
            _weapon.ReloadStarted -= OnReloadStarted;
            _weapon.ActiveReloadJudged -= OnActiveReloadJudged;
            _weapon.WeaponChanged -= OnWeaponChanged;
        }

        // 破棄済みの部品も渡せないよう、Unity の == で調べる。
        private static void RequireNotNull(Component component, string paramName)
        {
            if (component == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        private void RequireOnSelf(Component component, string paramName)
        {
            if (component.gameObject != gameObject)
            {
                throw new ArgumentException($"{paramName} must be on '{name}'", paramName);
            }
        }

        private string MissingReferences()
        {
            return string.Join(", ", Missing(_mover, _weapon, _ads, _cover, _blindFire, _damageable, _rig));
        }

        private static List<string> Missing(
            PlayerController mover,
            WeaponController weapon,
            AimDownSights ads,
            CoverController cover,
            BlindFireController blindFire,
            Damageable damageable,
            ViewRig rig)
        {
            var missing = new List<string>(7);
            AddIfMissing(missing, mover, nameof(PlayerController));
            AddIfMissing(missing, weapon, nameof(WeaponController));
            AddIfMissing(missing, ads, nameof(AimDownSights));
            AddIfMissing(missing, cover, nameof(CoverController));
            AddIfMissing(missing, blindFire, nameof(BlindFireController));
            AddIfMissing(missing, damageable, nameof(Fps.Runtime.Damageable));
            AddIfMissing(missing, rig, nameof(ViewRig));
            return missing;
        }

        private static void AddIfMissing(List<string> missing, Component component, string typeName)
        {
            if (component == null)
            {
                missing.Add(typeName);
            }
        }

        private static void Set(ref PlayerActionConditions flags, PlayerActionConditions flag, bool on)
        {
            if (on)
            {
                flags |= flag;
            }
        }
    }
}
