using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Fps.Net
{
    /// <summary>
    /// 接続したクライアントごとのプレイヤー。チームと体力をサーバーから受け取って見た目に反映し、
    /// サーバーの指示でテレポートする。Attack で撃つと射撃を <see cref="FireServerRpc"/> で送るだけで、
    /// 当たり判定 (ラグ補償) とダメージを含む試合の判断はすべて <see cref="NetTeamDeathmatch"/> (その先の
    /// <see cref="TeamDeathmatchSession"/>) が行う。
    /// <para>
    /// 視点 (一人称・三人称共通): プレハブは子 <see cref="ViewName"/> (目。pitch を回し射撃の始点になる) とその子のカメラ
    /// (Camera と AudioListener 付き)、本体の <see cref="ViewRig"/> と <see cref="FirstPersonLook"/> を無効のまま持つ。
    /// 自分のプレイヤー (オーナー) で画面があるときだけ有効にし、シーンの <see cref="NetSpectatorCamera"/> を止める。
    /// 一人称か三人称かは ViewRig の Mode で決まり、射撃はどちらでも目から画面中央が指す点へ撃つ。
    /// </para>
    /// <para>
    /// HUD も同じ条件 (<see cref="NetStartup.ShouldCreateHud"/>) で生成時に <see cref="NetHud.Create"/> で作り、消滅時に消す。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetPlayer : NetworkBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = "Player/Attack";

        /// <summary>参加前を表す <see cref="ServerPlayerId"/>。</summary>
        public const int NotJoined = -1;

        /// <summary>
        /// 目 (一人称カメラと射撃の始点) の、本体 (カプセルの中心) からの高さ (m)。
        /// サーバーの射撃の検査 (<see cref="NetTeamDeathmatch"/> の始点の遮蔽判定) も同じ高さを使う。
        /// </summary>
        public const float EyeHeight = 0.6f;

        /// <summary>pitch を回し射撃の始点になる目の子 GameObject の名前。カメラはこの子に置く。</summary>
        public const string ViewName = "View";

        /// <summary><see cref="ViewName"/> の子の、カメラを持つ GameObject の名前。</summary>
        public const string ViewCameraName = "ViewCamera";

        public static readonly Color AlphaColor = new Color(0.25f, 0.5f, 1f);
        public static readonly Color BravoColor = new Color(1f, 0.3f, 0.25f);

        /// <summary>
        /// サーバーにテレポートさせられたとき、所有クライアント (ホストなら自分) で呼ばれる。
        /// 予測中の位置は <see cref="NetworkPlayerMotor"/> がサーバーの確定状態との照合で置き直すので、ここでは通知だけ受け取れる。
        /// </summary>
        public event Action<Vector3, Quaternion> Teleported;

        private readonly NetworkVariable<Team> _team = new NetworkVariable<Team>(Team.Alpha);

        // 0 は倒れているか、まだ試合に参加していないことを表す
        private readonly NetworkVariable<int> _health = new NetworkVariable<int>(0);

        private NetworkPlayerMotor _motor;
        private NetPlayerWeapon _weapon;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider[] _colliders = Array.Empty<Collider>();
        private Material[] _ownedMaterials = Array.Empty<Material>();
        private InputAction _attackAction;
        private bool _enabledAttackAction;

        // 視点。View (目) は射撃の始点にも使うので、カメラを有効にしないプレイヤーでも解決しておく
        private Transform _view;
        private Camera _viewCamera;
        private AudioListener _viewListener;
        private FirstPersonLook _look;
        private ViewRig _rig;
        private bool _hasLocalView;
        private bool _viewResolved;

        // HUD を作ったときだけ。自分のプレイヤーで画面があるときに作る
        private NetHud _hud;

        public Team Team => _team.Value;
        public int Health => _health.Value;
        public bool IsAlive => _health.Value > 0;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読するチームの同期値。</summary>
        internal NetworkVariable<Team> TeamVariable => _team;

        /// <summary><see cref="NetHud"/> が OnValueChanged を購読する体力の同期値。</summary>
        internal NetworkVariable<int> HealthVariable => _health;

        /// <summary>このプレイヤーの HUD。自分のプレイヤーで画面があるときだけあり、他では null。</summary>
        public NetHud LocalHud => _hud;

        /// <summary>同じオブジェクトの武器 (弾倉・リロード)。プレハブに無ければ null。</summary>
        public NetPlayerWeapon Weapon => _weapon;

        /// <summary>サーバーでだけ使う、試合でのプレイヤー id。参加前は <see cref="NotJoined"/>。</summary>
        public int ServerPlayerId { get; internal set; } = NotJoined;

        /// <summary>pitch を回し射撃の始点になる目。プレハブに無ければ null。</summary>
        public Transform View
        {
            get
            {
                EnsureViewResolved();
                return _view;
            }
        }

        /// <summary>視点 (カメラ・音声・視点操作) が有効か。自分のプレイヤーで画面があるときだけ true。</summary>
        public bool HasLocalView => _hasLocalView;

        private void Awake()
        {
            _motor = GetComponent<NetworkPlayerMotor>();
            _weapon = GetComponent<NetPlayerWeapon>();
            _renderers = GetComponentsInChildren<Renderer>(true);

            // CharacterController の有効・無効は NetworkPlayerMotor が役割ごとに決めるので、ここでは触らない
            _colliders = Array.FindAll(GetComponentsInChildren<Collider>(true), c => !(c is CharacterController));

            ResolveView();
        }

        public override void OnNetworkSpawn()
        {
            _team.OnValueChanged += HandleTeamChanged;
            _health.OnValueChanged += HandleHealthChanged;
            ApplyVisuals();

            SetLocalView(NetStartup.ShouldEnableFirstPersonView(IsOwner, Application.isBatchMode, NetStartup.IsDedicatedServerBuild));

            if (IsOwner)
            {
                ResolveAttackAction();
            }

            if (NetStartup.ShouldCreateHud(IsOwner, Application.isBatchMode, NetStartup.IsDedicatedServerBuild))
            {
                CreateHud();
            }
        }

        public override void OnNetworkDespawn()
        {
            _team.OnValueChanged -= HandleTeamChanged;
            _health.OnValueChanged -= HandleHealthChanged;
            ReleaseAttackAction();
            SetLocalView(false);
            DestroyHud();

            if (IsServer && NetTeamDeathmatch.Current != null)
            {
                NetTeamDeathmatch.Current.ServerLeave(this);
            }
        }

        public override void OnDestroy()
        {
            // 消滅の通知を経ずに破棄された場合も、観戦用のカメラを戻す
            if (_hasLocalView)
            {
                _hasLocalView = false;
                NetSpectatorCamera.RemoveLocalView();
            }

            DestroyHud();

            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _ownedMaterials = Array.Empty<Material>();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _attackAction == null)
            {
                return;
            }

            // 届いている弾数で撃てないと分かっている射撃は送らない (弾倉が空ならリロードを頼む)。判断はサーバーが行う
            if (_attackAction.WasPressedThisFrame() && IsAlive && (_weapon == null || _weapon.PrepareFire()))
            {
                Fire();
            }
        }

        /// <summary>
        /// 1 発撃つ。目から画面中央が指す点への向きへ、いま見ている時点 (サーバーの時刻 − 補間の遅れ) と装備中の武器の Id を添えて
        /// サーバーに送るだけで、武器の値 (ダメージ・射程など) は送らない。当たり判定とダメージはサーバーが Catalog の値で行う。
        /// </summary>
        private void Fire()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0d)
            {
                Debug.LogWarning("[Net] cannot fire before the server time is known", this);
                return;
            }

            // 装備中の武器はサーバーから届く。届く前 (参加直後) や長すぎる Id は送ってもサーバーが捨てるので送らない
            var weaponId = _weapon != null ? _weapon.WeaponId : string.Empty;
            if (string.IsNullOrEmpty(weaponId) || !NetPlayerWeapon.FitsWeaponId(weaponId))
            {
                Debug.LogWarning("[Net] cannot fire before the server equips a weapon", this);
                return;
            }

            var origin = AimOrigin();
            var direction = AimDirection();

            // ホストは他人をサーバー上の今の位置で見ている (補間しない) ので遅れは 0
            var delay = IsServer ? 0 : NetworkPlayerMotor.InterpolationDelayTicks;
            var time = FireTiming.ViewTime(serverTime, NetworkPlayerMotor.TickRate, delay);
            Debug.Log("[Net] fire weapon=" + weaponId + " tick=" + time.Tick.ToString(CultureInfo.InvariantCulture)
                + " alpha=" + time.Alpha.ToString("F2", CultureInfo.InvariantCulture), this);
            FireServerRpc(new FixedString64Bytes(weaponId), origin, direction, time.Tick, time.Alpha);
        }

        /// <summary>
        /// 射撃の始点。目 (<see cref="View"/>) の位置で、本体の中心から <see cref="EyeHeight"/> 上になる。
        /// 三人称でもカメラではなく目から撃つ (サーバーは始点を目の高さで検査する)。View が無ければ同じ高さを計算で出す。
        /// </summary>
        public Vector3 AimOrigin()
        {
            EnsureViewResolved();
            return _view != null ? _view.position : transform.position + (Vector3.up * EyeHeight);
        }

        /// <summary>
        /// 射撃の向き。目から、画面中央 (カメラの正面) が指す点への向きで、pitch も含む (<see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/>)。
        /// 一人称ではカメラが目に重なるので目の forward と同じ。カメラが目の下に無ければ目の forward、View も無ければ本体の正面。
        /// </summary>
        public Vector3 AimDirection()
        {
            EnsureViewResolved();
            if (_view == null)
            {
                return transform.forward;
            }

            if (_viewCamera == null || _viewCamera.transform.parent != _view)
            {
                return _view.forward;
            }

            ViewRig.ResolveAim(
                _viewCamera.transform, _view, ViewRig.DefaultAimDistance, transform, Physics.DefaultRaycastLayers, out _, out var direction);
            return direction;
        }

        /// <summary>
        /// 視点を有効・無効にする。有効にするとカメラ・AudioListener・<see cref="ViewRig"/>・<see cref="FirstPersonLook"/> を動かし、
        /// 一人称なら自分の体は影だけ描いて視界をふさがないようにし (三人称なら描く)、観戦用のカメラを止める。無効にすると元に戻す。
        /// </summary>
        internal void SetLocalView(bool enable)
        {
            EnsureViewResolved();

            if (enable && _viewCamera == null)
            {
                Debug.LogError("[Net] " + name + " has no camera under '" + ViewName + "/" + ViewCameraName
                    + "'; run Fps.Editor.NetSceneBuilder.Build. Keeping the spectator camera", this);
                enable = false;
            }

            if (_viewCamera != null)
            {
                _viewCamera.enabled = enable;
            }

            if (_viewListener != null)
            {
                _viewListener.enabled = enable;
            }

            if (_look != null)
            {
                // 有効にするとカーソルをロックし、無効にすると外す (FirstPersonLook の OnEnable / OnDisable)
                _look.enabled = enable;
                if (enable && !_look.enabled)
                {
                    // 参照か Look アクションを解決できなかった。理由は FirstPersonLook がエラーに出している
                    Debug.LogWarning("[Net] " + name + ": FirstPersonLook could not start; the view cannot be turned", this);
                }
            }
            else if (enable)
            {
                Debug.LogWarning("[Net] " + name + " has no FirstPersonLook; the view cannot be turned", this);
            }

            if (_rig != null)
            {
                // 有効にすると LateUpdate でカメラを視点ごとの位置へ置く。Edit Mode では OnEnable が走らないので体の描き方はここで決める
                _rig.enabled = enable;
                _rig.ApplyBodyVisibility(enable);
            }
            else
            {
                var shadowMode = enable ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                // 生成と消滅のときだけ呼ばれるので、Awake を経ない場合も考えてその場で集める
                foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (bodyRenderer != null)
                    {
                        bodyRenderer.shadowCastingMode = shadowMode;
                    }
                }
            }

            if (enable == _hasLocalView)
            {
                return;
            }

            _hasLocalView = enable;
            if (enable)
            {
                NetSpectatorCamera.AddLocalView();
                Debug.Log("[Net] " + (_rig != null ? _rig.Mode : ViewMode.FirstPerson) + " view enabled on " + name, this);
            }
            else
            {
                NetSpectatorCamera.RemoveLocalView();
            }
        }

        // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で解決する
        private void EnsureViewResolved()
        {
            if (!_viewResolved)
            {
                ResolveView();
            }
        }

        private void ResolveView()
        {
            _viewResolved = true;
            _look = GetComponent<FirstPersonLook>();
            _rig = GetComponent<ViewRig>();
            _viewCamera = FirstPersonLook.FindChildCamera(transform);

            // 目は ViewName の子。古い構成 (カメラが目を兼ねる) ではカメラ自身
            _view = transform.Find(ViewName);
            if (_view == null && _viewCamera != null)
            {
                _view = _viewCamera.transform;
            }

            _viewListener = _viewCamera != null ? _viewCamera.GetComponent<AudioListener>() : null;
        }

        /// <summary>サーバーから、試合で決まったチームと体力を書き込む。</summary>
        internal void ServerSetState(Team team, int health)
        {
            RequireServer();
            _team.Value = team;
            _health.Value = health;
        }

        /// <summary>
        /// サーバーで position へ瞬間移動させる。位置の同期は <see cref="NetworkPlayerMotor"/> に任せ
        /// (予測の照合・補間の飛ばしもそちら)、所有クライアントには <see cref="Teleported"/> を出させる。
        /// </summary>
        internal void ServerTeleport(Vector3 position)
        {
            RequireServer();
            var rotation = transform.rotation;
            if (_motor != null && _motor.IsSpawned)
            {
                _motor.ServerTeleport(position);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            TeleportedRpc(position, rotation);
        }

        [Rpc(SendTo.Owner)]
        private void TeleportedRpc(Vector3 position, Quaternion rotation)
        {
            Teleported?.Invoke(position, rotation);
        }

        /// <summary>
        /// 射撃をサーバーへ送る。weaponId は撃った武器の Id で、武器の値は送らない (サーバーが Catalog から引く)。
        /// Catalog に無い・サーバーが持たせている武器と違う Id の射撃は捨てられる。
        /// clientTick + interpolationAlpha は撃った側が他人を見ていた時点 (サーバーの tick)。
        /// サーバーはその時点まで巻き戻して当たり判定をする (<see cref="NetTeamDeathmatch.ServerFire"/>)。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void FireServerRpc(
            FixedString64Bytes weaponId, Vector3 origin, Vector3 direction, int clientTick, float interpolationAlpha, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored shot from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerFire(this, weaponId.ToString(), origin, direction, new ShotTime(clientTick, interpolationAlpha));
            }
        }

        private void HandleTeamChanged(Team previous, Team current)
        {
            ApplyVisuals();
        }

        private void HandleHealthChanged(int previous, int current)
        {
            ApplyVisuals();
        }

        /// <summary>チームの色を塗り、倒れている間は見た目と当たりを消す。</summary>
        private void ApplyVisuals()
        {
            var alive = IsAlive;
            var color = _team.Value == Team.Bravo ? BravoColor : AlphaColor;

            if (_ownedMaterials.Length != _renderers.Length)
            {
                // renderer.material は複製を作るので、自分で破棄できるよう控えておく
                _ownedMaterials = new Material[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                {
                    _ownedMaterials[i] = _renderers[i].material;
                }
            }

            for (var i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = alive;
                _ownedMaterials[i].color = color;
            }

            foreach (var hitbox in _colliders)
            {
                hitbox.enabled = alive;
            }
        }

        private void CreateHud()
        {
            if (_hud != null)
            {
                return;
            }

            try
            {
                _hud = NetHud.Create(this);
                Debug.Log("[Net] HUD created for " + name, this);
            }
            catch (InvalidOperationException e)
            {
                // HUD が無くても遊べるので、プレイヤーの生成は止めない
                Debug.LogError("[Net] could not create the HUD: " + e.Message, this);
            }
        }

        private void DestroyHud()
        {
            if (_hud != null)
            {
                Destroy(_hud.gameObject);
            }

            _hud = null;
        }

        private void ResolveAttackAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var attack = actions != null ? actions.FindAction(AttackActionPath) : null;
            if (attack == null)
            {
                Debug.LogWarning("[Net] input action '" + AttackActionPath + "' was not found; firing is disabled");
                return;
            }

            _attackAction = attack;
            if (!_attackAction.enabled)
            {
                _attackAction.Enable();
                _enabledAttackAction = true;
            }
        }

        private void ReleaseAttackAction()
        {
            if (_enabledAttackAction && _attackAction != null)
            {
                _attackAction.Disable();
            }

            _enabledAttackAction = false;
            _attackAction = null;
        }

        private void RequireServer()
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("only the server may change " + name);
            }
        }
    }
}
