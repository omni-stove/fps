using System;
using System.Collections.Generic;
using System.Text;
using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// プレイヤーが持つ武器の並び (ロードアウト) と、今持っている武器の弾倉・予備弾・リロード。
    /// 武器はサーバーだけが <see cref="ServerWeapon"/> を並べた <see cref="Loadout{T}"/> で持ち、今の武器の弾数・リロード中かどうか・
    /// 持っている番号・持ち替え中かどうかを持ち主だけが読める NetworkVariable で配る (他人には送らない)。
    /// 持ち主が Reload を押すと <see cref="ReloadServerRpc"/>、Next / Previous を押すと持ち替え先の武器の Id を
    /// <see cref="SwitchWeaponServerRpc"/> で送り、サーバーがリロード・持ち替えを判定する。
    /// </summary>
    /// <remarks>
    /// 武器の設定 (<see cref="WeaponSpec"/>) は <see cref="NetTeamDeathmatch"/> がロードアウトのアセット (<see cref="LoadoutDefinition"/>) から引き、
    /// 出現・リスポーンのたびに <see cref="ServerEquip"/> で渡す。
    /// 今持っている武器の Id (<see cref="WeaponId"/>) は全員に配り、持ち主は射撃の RPC にこの Id を載せる (数値は送らない)。
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
        public const string ReloadActionPath = InputActionPaths.Reload;

        /// <summary>Assets/InputSystem_Actions.inputactions の Next アクション。次の番号の武器に持ち替える。</summary>
        public const string NextActionPath = "Player/Next";

        /// <summary>Assets/InputSystem_Actions.inputactions の Previous アクション。前の番号の武器に持ち替える。</summary>
        public const string PreviousActionPath = "Player/Previous";

        /// <summary>
        /// 持てる武器の最大数。番号と数を <see cref="NetWeaponState"/> の byte で送るため。
        /// </summary>
        public const int MaxWeaponCount = byte.MaxValue;

        /// <summary>
        /// 武器の Id として同期・送信できる最大バイト数 (<see cref="FixedString64Bytes"/>)。
        /// UUID の Id (36 バイト) は <see cref="FixedString32Bytes"/> (29 バイト) に収まらない。
        /// </summary>
        public static int MaxWeaponIdBytes => FixedString64Bytes.UTF8MaxLengthInBytes;

        /// <summary>
        /// 今持っている武器の Id。全員が読める (持ち替えの見た目に使えるよう)。値は Id だけで、弾数は <see cref="_state"/> で持ち主にだけ送る。
        /// </summary>
        private readonly NetworkVariable<FixedString64Bytes> _weaponId = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// 今の武器の弾倉の残り・予備弾・リロード中かどうかと、番号・持ち替え中かどうか。持ち主 (とサーバー) だけが読める。
        /// 他人の弾数は表示に要らず、送ると相手のリロードや持ち替えの隙が分かってしまうため。
        /// </summary>
        private readonly NetworkVariable<NetWeaponState> _state = new NetworkVariable<NetWeaponState>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        private NetPlayer _player;
        private Loadout<ServerWeapon> _loadout;
        private InputAction _reloadAction;
        private bool _enabledReloadAction;
        private InputAction _nextAction;
        private bool _enabledNextAction;
        private InputAction _previousAction;
        private bool _enabledPreviousAction;

        /// <summary>
        /// 持ち主で弾数・リロード中かどうか・持っている番号・持ち替え中かどうかが変わったときに呼ばれる (サーバーから届いたとき)。
        /// </summary>
        public event Action<NetWeaponState> StateChanged;

        /// <summary>
        /// 持ち主で、サーバーから届いた状態でリロードか持ち替えが始まったとき (<see cref="StartsReloadOrSwitch"/>) に、届いた状態を渡して呼ばれる。
        /// 弾倉が空になったときのサーバーの自動リロードも含む。
        /// </summary>
        public event Action<NetWeaponState> ReloadOrSwitchStarted;

        /// <summary>サーバーから届いた武器の状態。持ち主とサーバーでだけ意味がある (他人の分は届かず既定値のまま)。</summary>
        public NetWeaponState State => _state.Value;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読する武器の状態の同期値。</summary>
        internal NetworkVariable<NetWeaponState> StateVariable => _state;

        public int AmmoInMagazine => _state.Value.AmmoInMagazine;
        public int ReserveAmmo => _state.Value.ReserveAmmo;
        public bool IsReloading => _state.Value.IsReloading;

        /// <summary>サーバーから届いた、今持っている武器の番号。</summary>
        public int EquippedIndex => _state.Value.EquippedIndex;

        /// <summary>サーバーから届いた、持ち替え中かどうか。</summary>
        public bool IsSwitching => _state.Value.IsSwitching;

        /// <summary>サーバーが持つ武器の並び。<see cref="ServerEquip"/> の前とクライアントでは null。</summary>
        public Loadout<ServerWeapon> Loadout => _loadout;

        /// <summary>サーバーが持つ、今持っている武器 (持ち替え中は持ち替え先)。<see cref="ServerEquip"/> の前とクライアントでは null。</summary>
        public ServerWeapon ServerWeapon => _loadout?.Current;

        /// <summary>
        /// サーバーで、今持っている武器 (持ち替え中は持ち替え先) の設定。<see cref="ServerEquip"/> の前とクライアントでは null。
        /// ダメージ・射程・連射間隔はクライアントの申告ではなくこれを使う。
        /// </summary>
        public WeaponSpec CurrentSpec => _loadout?.Current.Spec;

        /// <summary>
        /// 今持っている武器の Id。サーバーでは <see cref="CurrentSpec"/> の Id、クライアントではサーバーから届いた値。
        /// 装備前 (届く前) は空文字。持ち主はこれを射撃の RPC に載せる。
        /// </summary>
        public string WeaponId
        {
            get
            {
                var spec = CurrentSpec;
                if (spec != null)
                {
                    return spec.Id;
                }

                return _weaponId.Value.ToString();
            }
        }

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
                ResolveInputActions();
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            ReleaseInputActions();
        }

        public override void OnDestroy()
        {
            SetLoadout(null);
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && _loadout != null)
            {
                ServerTick(Time.deltaTime);
            }

            if (!IsOwner)
            {
                return;
            }

            if (_reloadAction != null && _reloadAction.WasPressedThisFrame())
            {
                RequestReload();
            }

            if (_nextAction != null && _nextAction.WasPressedThisFrame())
            {
                RequestNextWeapon();
            }
            else if (_previousAction != null && _previousAction.WasPressedThisFrame())
            {
                RequestPreviousWeapon();
            }
        }

        /// <summary>
        /// サーバーで、持ち替えと今の武器のリロードを deltaSeconds 進める。0 以上の有限値。装備前なら何もしない。
        /// <see cref="Update"/> がフレームの経過時間で呼ぶ。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public void ServerTick(float deltaSeconds)
        {
            RequireServerSide();
            if (_loadout == null)
            {
                return;
            }

            var wasSwitching = _loadout.IsSwitching;
            _loadout.Tick(deltaSeconds);

            // リロードの完了は Changed で SyncState される
            _loadout.Current.Tick(deltaSeconds);

            // 持ち替えの終わりには Loadout のイベントが無いので、ここで配る
            if (wasSwitching != _loadout.IsSwitching)
            {
                SyncState();
            }
        }

        /// <summary>
        /// 持ち主で、撃つ前に呼ぶ。サーバーから届いた状態で弾があれば true (射撃を送ってよい)。
        /// リロード中か持ち替え中なら false。弾倉が空なら射撃を送る代わりにリロードを頼んで false。
        /// サーバーへの無駄な RPC を減らすためのもので、最終的な判断はサーバーの <see cref="ServerTryFire"/> が行う。
        /// </summary>
        public bool PrepareFire()
        {
            var state = _state.Value;
            if (state.IsReloading || state.IsSwitching)
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
        /// 持ち主で、サーバーにリロードを頼む。届いている状態でリロード中か持ち替え中か予備弾が無ければ送らず false。
        /// 弾倉が満タンかどうかはサーバーが確かめる。
        /// </summary>
        public bool RequestReload()
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            var state = _state.Value;
            if (state.IsReloading || state.IsSwitching || state.ReserveAmmo == 0 || (_player != null && !_player.IsAlive))
            {
                return false;
            }

            ReloadServerRpc();
            return true;
        }

        /// <summary>
        /// 持ち主で、サーバーに weaponId の武器への持ち替えを頼む。空・<see cref="MaxWeaponIdBytes"/> に収まらない・
        /// 今持っている武器 (<see cref="WeaponId"/>) と同じ・倒れている、のどれかなら送らず false。
        /// ロードアウトにあるかどうかはサーバーが確かめる。持ち替え中でも送る (持ち替えの時間はサーバーでそこから数え直す)。
        /// </summary>
        public bool RequestSwitch(string weaponId)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            if (string.IsNullOrEmpty(weaponId) || !FitsWeaponId(weaponId)
                || string.Equals(weaponId, WeaponId, StringComparison.Ordinal)
                || (_player != null && !_player.IsAlive))
            {
                return false;
            }

            SwitchWeaponServerRpc(new FixedString64Bytes(weaponId));
            return true;
        }

        /// <summary>
        /// 持ち主で、試合のロードアウト (<see cref="NetTeamDeathmatch.Loadout"/>) で今の武器の次 (最後の次は先頭) の武器への持ち替えを頼む。
        /// 1 本しか無い・今の武器が分からない・ロードアウトが読めないなら送らず false。
        /// </summary>
        public bool RequestNextWeapon()
        {
            return RequestNeighbour(1);
        }

        /// <summary>持ち主で、試合のロードアウトで今の武器の前 (先頭の前は最後) の武器への持ち替えを頼む。送れなければ false。</summary>
        public bool RequestPreviousWeapon()
        {
            return RequestNeighbour(-1);
        }

        private bool RequestNeighbour(int step)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            var target = NeighbourWeaponId(ResolveClientSpecs(), WeaponId, step);
            return target != null && RequestSwitch(target);
        }

        /// <summary>
        /// specs の中で currentId の武器から step だけ進めた (端では反対側へ回り込む) 武器の Id。
        /// specs が null か 1 本以下、currentId が specs に無い、step が 0 なら null。
        /// </summary>
        internal static string NeighbourWeaponId(IReadOnlyList<WeaponSpec> specs, string currentId, int step)
        {
            if (specs == null || specs.Count <= 1 || step == 0)
            {
                return null;
            }

            var current = IndexOf(specs, currentId);
            if (current < 0)
            {
                return null;
            }

            var count = specs.Count;
            var next = ((current + (step % count)) % count + count) % count;
            return specs[next].Id;
        }

        // クライアントは武器の並びを持たないので、サーバーが全員に配っているのと同じロードアウトのアセットから引く
        private IReadOnlyList<WeaponSpec> ResolveClientSpecs()
        {
            var match = NetTeamDeathmatch.Current;
            var loadout = match != null ? match.Loadout : null;
            if (loadout == null)
            {
                Debug.LogWarning("[Net] cannot switch weapons without the match's loadout", this);
                return null;
            }

            try
            {
                return loadout.ToSpecs();
            }
            catch (WeaponDefinitionException e)
            {
                Debug.LogWarning("[Net] cannot switch weapons with an invalid loadout: " + e.Message, this);
                return null;
            }
        }

        private static int IndexOf(IReadOnlyList<WeaponSpec> specs, string weaponId)
        {
            if (weaponId == null)
            {
                return -1;
            }

            for (var i = 0; i < specs.Count; i++)
            {
                if (specs[i] != null && string.Equals(specs[i].Id, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>武器の Id が <see cref="MaxWeaponIdBytes"/> (射撃の RPC と <see cref="WeaponId"/> の同期) に収まるか。</summary>
        public static bool FitsWeaponId(string weaponId)
        {
            return weaponId != null && Encoding.UTF8.GetByteCount(weaponId) <= MaxWeaponIdBytes;
        }

        /// <summary>
        /// サーバーで、specs の武器をこの並びで 1 本ずつ満タンで持たせ、0 番を持った状態にする。
        /// 前の武器 (リロード中・持ち替え中を含む) は捨てる。
        /// </summary>
        /// <param name="specs">持たせる武器。1 本以上 <see cref="MaxWeaponCount"/> 本以下で、null 要素は不可。</param>
        /// <param name="switchSeconds">持ち替えにかかる秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentNullException">specs が null。</exception>
        /// <exception cref="ArgumentException">
        /// specs が空・多すぎる・null を含む、または Id が <see cref="MaxWeaponIdBytes"/> に収まらない武器がある。
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">switchSeconds が負か有限値でない。</exception>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public void ServerEquip(IReadOnlyList<WeaponSpec> specs, float switchSeconds)
        {
            if (specs == null)
            {
                throw new ArgumentNullException(nameof(specs));
            }

            if (specs.Count == 0)
            {
                throw new ArgumentException("specs must not be empty", nameof(specs));
            }

            if (specs.Count > MaxWeaponCount)
            {
                throw new ArgumentException("specs has " + specs.Count + " weapons but at most " + MaxWeaponCount
                    + " can be synced", nameof(specs));
            }

            var weapons = new ServerWeapon[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null)
                {
                    throw new ArgumentException("specs must not contain null (index " + i + ")", nameof(specs));
                }

                if (!FitsWeaponId(spec.Id))
                {
                    throw new ArgumentException("weapon id '" + spec.Id + "' is longer than "
                        + MaxWeaponIdBytes + " bytes and cannot be synced", nameof(specs));
                }

                weapons[i] = new ServerWeapon(spec);
            }

            RequireServerSide();

            // 引数が不正なら Loadout が例外を投げるので、前の武器を外すのは作れてから
            var loadout = new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon);
            SetLoadout(loadout);
            SyncState();
        }

        /// <summary>
        /// サーバーで、全武器の弾倉と予備弾を満タンに戻してリロード中ならやめ、持ち替え中でも 0 番を持った状態に戻す。
        /// リスポーンと試合の開始時に呼ぶ。装備前なら何もしない。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public void ServerRefill()
        {
            RequireServerSide();
            if (_loadout == null)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.Refill();
            }

            if (_loadout.CurrentIndex != 0 || _loadout.IsSwitching)
            {
                // TrySelect(0) では持ち替えの待ち時間が始まり、プレイヤーの持ち替えとしてログも出る。
                // 出現時は待たずに 0 番を構えた状態にしたいので、同じ武器で並びを作り直す
                SetLoadout(new Loadout<ServerWeapon>(_loadout.Items, _loadout.SwitchSeconds, HolsterWeapon));
            }

            SyncState();
        }

        /// <summary>
        /// サーバーで、今の武器で 1 発ぶん消費する。撃てたら <see cref="ShotRejection.None"/>。装備前なら <see cref="ShotRejection.NoWeapon"/>、
        /// 持ち替え中なら <see cref="ShotRejection.Switching"/>、リロード中なら <see cref="ShotRejection.Reloading"/>、
        /// 弾倉が空なら <see cref="ShotRejection.OutOfAmmo"/> (予備弾があればリロードを始める)。
        /// </summary>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public ShotRejection ServerTryFire()
        {
            RequireServerSide();
            if (_loadout == null)
            {
                return ShotRejection.NoWeapon;
            }

            if (_loadout.IsSwitching)
            {
                return ShotRejection.Switching;
            }

            return _loadout.Current.TryFire();
        }

        /// <summary>
        /// サーバーで、senderClientId から届いたリロードの要求を処理する。持ち主以外からの要求は警告を出して捨てる。
        /// 持ち替え中は始めない。リロードを始めたら true。
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

            return _loadout != null && !_loadout.IsSwitching && _loadout.Current.TryStartReload();
        }

        /// <summary>
        /// サーバーで、senderClientId から届いた weaponId の武器への持ち替えの要求を処理する。持ち主以外からの要求と、
        /// このプレイヤーのロードアウトに無い Id (大文字・小文字は区別する) は警告を出して捨てる。今の武器と同じなら何もしない。
        /// 持ち替えを始めたら true (今の武器のリロードはやめ、各武器の残弾はそのまま)。
        /// </summary>
        /// <remarks>
        /// 連射の制限 (<see cref="ShotBudget"/>) は <see cref="ServerLoadout.TryFire"/> が持ち替え先の武器で最初に撃ったときに
        /// その武器の間隔で作り直す。持ち替え前の Id で届いた射撃はそこで <see cref="ShotRejection.WeaponMismatch"/> になる。
        /// </remarks>
        /// <exception cref="InvalidOperationException">Spawn 済みのクライアントで呼んだ。</exception>
        public bool ServerHandleSwitchRequest(ulong senderClientId, string weaponId)
        {
            RequireServerSide();
            if (senderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored switch from client " + senderClientId + " for player owned by " + OwnerClientId, this);
                return false;
            }

            if (_loadout == null)
            {
                return false;
            }

            var index = IndexOfWeapon(weaponId);
            if (index < 0)
            {
                Debug.LogWarning("[Net] ignored switch client=" + senderClientId + " weaponIdLength=" + (weaponId?.Length ?? 0) + " not in the loadout", this);
                return false;
            }

            return _loadout.TrySelect(index);
        }

        private int IndexOfWeapon(string weaponId)
        {
            if (weaponId == null)
            {
                return -1;
            }

            var items = _loadout.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void ReloadServerRpc(RpcParams rpcParams = default)
        {
            ServerHandleReloadRequest(rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void SwitchWeaponServerRpc(FixedString64Bytes weaponId, RpcParams rpcParams = default)
        {
            ServerHandleSwitchRequest(rpcParams.Receive.SenderClientId, weaponId.ToString());
        }

        private void SetLoadout(Loadout<ServerWeapon> loadout)
        {
            if (_loadout != null)
            {
                _loadout.Switched -= HandleSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Changed -= SyncState;
                    weapon.ReloadStarted -= HandleReloadStarted;
                    weapon.Reloaded -= HandleReloaded;
                }
            }

            _loadout = loadout;
            if (_loadout != null)
            {
                _loadout.Switched += HandleSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Changed += SyncState;
                    weapon.ReloadStarted += HandleReloadStarted;
                    weapon.Reloaded += HandleReloaded;
                }
            }
        }

        private static void HolsterWeapon(ServerWeapon weapon)
        {
            weapon.CancelReload();
        }

        /// <summary>サーバーの今の武器の状態を NetworkVariable に写す。Spawn 前は写さない (Spawn 時に写す)。</summary>
        private void SyncState()
        {
            if (!IsSpawned || !IsServer || _loadout == null)
            {
                return;
            }

            var weapon = _loadout.Current;
            var state = new NetWeaponState(
                weapon.AmmoInMagazine,
                weapon.ReserveAmmo,
                weapon.IsReloading,
                (byte)_loadout.CurrentIndex,
                (byte)_loadout.Count,
                _loadout.IsSwitching);
            if (!_state.Value.Equals(state))
            {
                _state.Value = state;
            }

            var weaponId = new FixedString64Bytes(weapon.Spec.Id);
            if (!_weaponId.Value.Equals(weaponId))
            {
                _weaponId.Value = weaponId;
            }
        }

        private void HandleSwitched(int index)
        {
            Debug.Log("[Net] switch client=" + OwnerClientId + " index=" + index + " weapon=" + _loadout.Items[index].Spec.Id, this);
            SyncState();
        }

        // しまった武器のリロードは HolsterWeapon でやめ、Tick も今の武器だけなので、リロードが起きるのは今の武器だけ
        private void HandleReloadStarted(bool automatic)
        {
            var weapon = _loadout.Current;
            Debug.Log("[Net] reload start client=" + OwnerClientId + " auto=" + automatic + " ammo=" + weapon.AmmoInMagazine
                + " reserve=" + weapon.ReserveAmmo, this);
        }

        private void HandleReloaded()
        {
            var weapon = _loadout.Current;
            Debug.Log("[Net] reloaded client=" + OwnerClientId + " ammo=" + weapon.AmmoInMagazine + " reserve=" + weapon.ReserveAmmo, this);
        }

        /// <summary>
        /// previous から current への変化で、リロードか持ち替えが始まったか (リロード中・持ち替え中になったか、持っている番号が変わったか)。
        /// 装備前 (武器の数が 0) からの最初の同期は始まりとみなさない。
        /// </summary>
        public static bool StartsReloadOrSwitch(NetWeaponState previous, NetWeaponState current)
        {
            if (previous.WeaponCount == 0)
            {
                return false;
            }

            return (current.IsReloading && !previous.IsReloading)
                || (current.IsSwitching && !previous.IsSwitching)
                || current.EquippedIndex != previous.EquippedIndex;
        }

        private void HandleStateChanged(NetWeaponState previous, NetWeaponState current)
        {
            StateChanged?.Invoke(current);
            if (StartsReloadOrSwitch(previous, current))
            {
                ReloadOrSwitchStarted?.Invoke(current);
            }
        }

        private void ResolveInputActions()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            _reloadAction = ResolveAction(actions, ReloadActionPath, "manual reload is disabled (an empty magazine still reloads automatically)",
                out _enabledReloadAction);
            _nextAction = ResolveAction(actions, NextActionPath, "switching to the next weapon is disabled", out _enabledNextAction);
            _previousAction = ResolveAction(actions, PreviousActionPath, "switching to the previous weapon is disabled",
                out _enabledPreviousAction);
        }

        private static InputAction ResolveAction(InputActionAsset actions, string path, string disabledEffect, out bool enabledHere)
        {
            enabledHere = false;
            var action = actions != null ? actions.FindAction(path) : null;
            if (action == null)
            {
                Debug.LogWarning("[Net] input action '" + path + "' was not found; " + disabledEffect);
                return null;
            }

            if (!action.enabled)
            {
                action.Enable();
                enabledHere = true;
            }

            return action;
        }

        private void ReleaseInputActions()
        {
            ReleaseAction(ref _reloadAction, ref _enabledReloadAction);
            ReleaseAction(ref _nextAction, ref _enabledNextAction);
            ReleaseAction(ref _previousAction, ref _enabledPreviousAction);
        }

        private static void ReleaseAction(ref InputAction action, ref bool enabledHere)
        {
            if (enabledHere && action != null)
            {
                action.Disable();
            }

            enabledHere = false;
            action = null;
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
