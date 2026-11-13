using System;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// プレイヤーの武器 (弾倉・予備弾・リロード)。弾数はサーバーだけが <see cref="ServerWeapon"/> で持ち、
    /// 弾倉の残り・予備弾・リロード中かどうかを持ち主だけが読める NetworkVariable で配る (他人には送らない)。
    /// 持ち主が Reload を押すと <see cref="ReloadServerRpc"/> を送り、サーバーがリロードを始める。
    /// </summary>
    /// <remarks>
    /// 武器の設定は <see cref="NetTeamDeathmatch"/> が試合の開始時に作り、参加時に <see cref="ServerEquip"/> で渡す。
    /// 射撃 1 発ぶんの消費は <see cref="NetTeamDeathmatch.ServerFire"/> が当たり判定の前に <see cref="ServerTryFire"/> で行う。
    /// <para>
    /// <c>Server*</c> の付く操作は、Spawn 済みならサーバーでしか呼べない。Spawn 前 (組み立て中・テスト) は
    /// サーバー側の状態だけを変え、同期はしない。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetPlayerWeapon : NetworkBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Reload アクション (キーボードの R)。</summary>
        public const string ReloadActionPath = "Player/Reload";

        /// <summary>武器の既定値。オフラインの WeaponController と同じ値にそろえる。</summary>
        public const int DefaultMagazineSize = 30;
        public const int DefaultReserveAmmo = 90;
        public const float DefaultReloadSeconds = 2f;

        /// <summary>
        /// 弾倉の残り・予備弾・リロード中かどうか。持ち主 (とサーバー) だけが読める。
        /// 他人の弾数は表示に要らず、送ると相手のリロードの隙が分かってしまうため。
        /// </summary>
        private readonly NetworkVariable<NetWeaponState> _state = new NetworkVariable<NetWeaponState>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        private NetPlayer _player;
        private ServerWeapon _weapon;
        private InputAction _reloadAction;
        private bool _enabledReloadAction;

        /// <summary>
        /// 持ち主で弾数かリロード中かどうかが変わったときに呼ばれる (サーバーから届いたとき)。
        /// </summary>
        public event Action<NetWeaponState> StateChanged;

        /// <summary>サーバーから届いた武器の状態。持ち主とサーバーでだけ意味がある (他人の分は届かず既定値のまま)。</summary>
        public NetWeaponState State => _state.Value;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読する武器の状態の同期値。</summary>
        internal NetworkVariable<NetWeaponState> StateVariable => _state;

        public int AmmoInMagazine => _state.Value.AmmoInMagazine;
        public int ReserveAmmo => _state.Value.ReserveAmmo;
        public bool IsReloading => _state.Value.IsReloading;

        /// <summary>サーバーが持つ武器。<see cref="ServerEquip"/> の前とクライアントでは null。</summary>
        public ServerWeapon ServerWeapon => _weapon;

        private void Awake()
        {
            _player = GetComponent<NetPlayer>();
        }

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += HandleStateChanged;

            if (IsServer)
            {
                // Spawn より前に装備していたら、その状態を配る
                SyncState();
            }

            if (IsOwner)
            {
                ResolveReloadAction();
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            ReleaseReloadAction();
        }

        public override void OnDestroy()
        {
            SetWeapon(null);
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && _weapon != null)
            {
                // リロードの完了は Changed で SyncState される
                _weapon.Tick(Time.deltaTime);
            }

            if (IsOwner && _reloadAction != null && _reloadAction.WasPressedThisFrame())
            {
                RequestReload();
            }
        }

        /// <summary>
        /// 持ち主で、撃つ前に呼ぶ。サーバーから届いた状態で弾があれば true (射撃を送ってよい)。
        /// リロード中なら false。弾倉が空なら射撃を送る代わりにリロードを頼んで false。
        /// サーバーへの無駄な RPC を減らすためのもので、最終的な判断はサーバーの <see cref="ServerTryFire"/> が行う。
        /// </summary>
        public bool PrepareFire()
        {
            var state = _state.Value;
            if (state.IsReloading)
            {
                return false;
            }

            if (state.AmmoInMagazine > 0)
            {
                return true;
            }

            RequestReload();
            return false;
        }

        /// <summary>
        /// 持ち主で、サーバーにリロードを頼む。届いている状態でリロード中か予備弾が無ければ送らず false。
        /// 弾倉が満タンかどうかはサーバーが確かめる。
        /// </summary>
        public bool RequestReload()
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            var state = _state.Value;
            if (state.IsReloading || state.ReserveAmmo == 0 || (_player != null && !_player.IsAlive))
            {
                return false;
            }

            ReloadServerRpc();
            return true;
        }

        /// <summary>サーバーで、spec の武器を満タンで持たせる。前の武器 (リロード中を含む) は捨てる。</summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public void ServerEquip(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            RequireServerSide();
            SetWeapon(new ServerWeapon(spec));
            SyncState();
        }

        /// <summary>サーバーで、弾倉と予備弾を満タンに戻し、リロード中ならやめる。リスポーンと試合の開始時に呼ぶ。装備前なら何もしない。</summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public void ServerRefill()
        {
            RequireServerSide();
            _weapon?.Refill();
        }

        /// <summary>
        /// サーバーで 1 発ぶん消費する。撃てたら <see cref="ShotRejection.None"/>。装備前なら <see cref="ShotRejection.NoWeapon"/>、
        /// リロード中なら <see cref="ShotRejection.Reloading"/>、弾倉が空なら <see cref="ShotRejection.OutOfAmmo"/>
        /// (予備弾があればリロードを始める)。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public ShotRejection ServerTryFire()
        {
            RequireServerSide();
            return _weapon != null ? _weapon.TryFire() : ShotRejection.NoWeapon;
        }

        /// <summary>
        /// サーバーで、senderClientId から届いたリロードの要求を処理する。持ち主以外からの要求は警告を出して捨てる。
        /// リロードを始めたら true。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public bool ServerHandleReloadRequest(ulong senderClientId)
        {
            RequireServerSide();
            if (senderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored reload from client " + senderClientId + " for player owned by " + OwnerClientId, this);
                return false;
            }

            return _weapon != null && _weapon.TryStartReload();
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void ReloadServerRpc(RpcParams rpcParams = default)
        {
            ServerHandleReloadRequest(rpcParams.Receive.SenderClientId);
        }

        private void SetWeapon(ServerWeapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.Changed -= SyncState;
                _weapon.ReloadStarted -= HandleReloadStarted;
                _weapon.Reloaded -= HandleReloaded;
            }

            _weapon = weapon;
            if (_weapon != null)
            {
                _weapon.Changed += SyncState;
                _weapon.ReloadStarted += HandleReloadStarted;
                _weapon.Reloaded += HandleReloaded;
            }
        }

        /// <summary>サーバーの武器の状態を NetworkVariable に写す。Spawn 前は写さない (Spawn 時に写す)。</summary>
        private void SyncState()
        {
            if (!IsSpawned || !IsServer || _weapon == null)
            {
                return;
            }

            var state = new NetWeaponState(_weapon.AmmoInMagazine, _weapon.ReserveAmmo, _weapon.IsReloading);
            if (!_state.Value.Equals(state))
            {
                _state.Value = state;
            }
        }

        private void HandleReloadStarted(bool automatic)
        {
            Debug.Log("[Net] reload start client=" + OwnerClientId + " auto=" + automatic + " ammo=" + _weapon.AmmoInMagazine
                + " reserve=" + _weapon.ReserveAmmo, this);
        }

        private void HandleReloaded()
        {
            Debug.Log("[Net] reloaded client=" + OwnerClientId + " ammo=" + _weapon.AmmoInMagazine + " reserve=" + _weapon.ReserveAmmo, this);
        }

        private void HandleStateChanged(NetWeaponState previous, NetWeaponState current)
        {
            StateChanged?.Invoke(current);
        }

        private void ResolveReloadAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var reload = actions != null ? actions.FindAction(ReloadActionPath) : null;
            if (reload == null)
            {
                Debug.LogWarning("[Net] input action '" + ReloadActionPath + "' was not found; manual reload is disabled"
                    + " (an empty magazine still reloads automatically)");
                return;
            }

            _reloadAction = reload;
            if (!_reloadAction.enabled)
            {
                _reloadAction.Enable();
                _enabledReloadAction = true;
            }
        }

        private void ReleaseReloadAction()
        {
            if (_enabledReloadAction && _reloadAction != null)
            {
                _reloadAction.Disable();
            }

            _enabledReloadAction = false;
            _reloadAction = null;
        }

        private void RequireServerSide()
        {
            if (IsSpawned && !IsServer)
            {
                throw new InvalidOperationException("only the server may change the weapon of " + name);
            }
        }
    }
}
