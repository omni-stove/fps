using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Fps.Net
{
    /// <summary>
    /// 接続したクライアントごとのプレイヤー。チームと体力をサーバーから受け取って見た目に反映し、
    /// サーバーの指示でテレポートする。Attack で撃つと (装備中の武器の <see cref="FireMode"/> に従い、Auto は押している間
    /// 連射間隔ごと、Semi は押すたびに) 射撃を <see cref="FireServerRpc"/> で送るだけで、
    /// 当たり判定 (ラグ補償) とダメージを含む試合の判断はすべて <see cref="NetTeamDeathmatch"/> (その先の
    /// <see cref="TeamDeathmatchSession"/>) が行う。
    /// <para>
    /// 視点 (一人称・三人称共通): プレハブは子 <see cref="ViewName"/> (目。pitch を回し射撃の始点になる) とその子のカメラ
    /// (Camera と AudioListener 付き)、本体の <see cref="ViewRig"/>・<see cref="FirstPersonLook"/>・<see cref="AimDownSights"/> を無効のまま持つ。
    /// 自分のプレイヤー (オーナー) で画面があるときだけ有効にし、シーンの <see cref="NetSpectatorCamera"/> を止める。
    /// 一人称か三人称かは ViewRig の Mode で決まり、射撃はどちらでも目から画面中央が指す点へ撃つ。
    /// </para>
    /// <para>
    /// HUD も同じ条件 (<see cref="NetStartup.ShouldCreateHud"/>) で生成時に <see cref="NetHud.Create"/> で作り、消滅時に消す。
    /// </para>
    /// <para>
    /// 倒れると見た目と当たりを消す。試合のルール (<see cref="NetTeamDeathmatch.SyncedRules"/>) のやられ演出が Collapse なら、
    /// 画面のあるクライアントでだけ <see cref="DeathBodyPresenter"/> が抜け殻をサーバーから届いた最後の被弾の向きへ倒し、リスポーンで消す。
    /// 抜け殻はローカルな見た目だけで、判定と同期には関わらない。
    /// </para>
    /// <para>
    /// 射撃の入力と <see cref="FireServerRpc"/> は .Fire.cs に分けてある。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class NetPlayer : NetworkBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = InputActionPaths.Attack;

        /// <summary>参加前を表す <see cref="ServerPlayerId"/>。</summary>
        public const int NotJoined = -1;

        /// <summary>
        /// 立っているときの目 (一人称カメラと射撃の始点) の、本体 (カプセルの中心) からの高さ (m)。
        /// しゃがむと <see cref="NetStance"/> が頭のてっぺんからの距離を保ったまま下げる。
        /// サーバーの射撃の検査 (<see cref="NetTeamDeathmatch"/> の始点の遮蔽判定) も同じ規則の高さ (<see cref="EyePosition"/>) を使う。
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

        // 最後に受けた弾の進む向き (単位ベクトル)。まだ撃たれていなければ zero。倒れた体を倒す向きにだけ使い、判定には使わない
        private readonly NetworkVariable<Vector3> _lastHitDirection = new NetworkVariable<Vector3>(Vector3.zero);

        // 0 は倒れているか、まだ試合に参加していないことを表す
        private readonly NetworkVariable<int> _health = new NetworkVariable<int>(0);

        private NetworkPlayerMotor _motor;
        private NetPlayerWeapon _weapon;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider[] _colliders = Array.Empty<Collider>();
        private Material[] _ownedMaterials = Array.Empty<Material>();
        private InputAction _attackAction;
        private bool _enabledAttackAction;

        // 装備中の武器の射撃モードと連射間隔。持ち主だけが ResolveTrigger で作る。_triggerWeaponId と _triggerLoadout はどの武器で引いたか
        private FireTrigger _trigger;
        private WeaponSpec _triggerSpec;
        private string _triggerWeaponId;
        private LoadoutDefinition _triggerLoadout;
        private string _triggerError;
        private bool _triggerResolved;

        // 視点。View (目) は射撃の始点にも使うので、カメラを有効にしないプレイヤーでも解決しておく
        private Transform _view;
        private Camera _viewCamera;
        private AudioListener _viewListener;
        private FirstPersonLook _look;
        private ViewRig _rig;
        private AimDownSights _ads;
        private bool _hasLocalView;
        private bool _viewResolved;

        // HUD を作ったときだけ。自分のプレイヤーで画面があるときに作る
        private NetHud _hud;

        // やられ演出。画面のあるクライアント (ホストを含む) でだけ付ける。倒れた tick の LateUpdate で出す
        private DeathBodyPresenter _deathBody;
        private bool _deathBodyPending;

        // 映している間は射撃の入力を読まない。プレハブに無ければ null
        private NetDeathCamera _deathCamera;
        private bool _deathCameraResolved;

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

        /// <summary>
        /// やられ演出のデスカム (<see cref="NetDeathCamera"/>) を映しているか。映している間は射撃の入力を読まない
        /// (連射の間隔も進めない)。カメラが自分の目に戻るまでは、狙った所へ撃てないため。
        /// </summary>
        public bool IsWatchingDeathCamera
        {
            get
            {
                ResolveDeathCamera();
                return _deathCamera != null && _deathCamera.IsActive;
            }
        }

        private void Awake()
        {
            _motor = GetComponent<NetworkPlayerMotor>();
            _weapon = GetComponent<NetPlayerWeapon>();
            ResolveDeathCamera();
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

            // NetBootstrap が止めるのは起動時のシーンだけなので、後から生成されるプレイヤーの演出はここで止める
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                HeadlessPresentation.DisableUnder(gameObject);
            }

            if (IsOwner)
            {
                ResolveAttackAction();
                if (_weapon != null)
                {
                    _weapon.ReloadOrSwitchStarted += HandleReloadOrSwitchStarted;
                }
            }

            if (NetStartup.ShouldCreateHud(IsOwner, Application.isBatchMode, NetStartup.IsDedicatedServerBuild))
            {
                CreateHud();
            }

            // サーバーだけのインスタンスには見る人がいないので作らない (専用サーバーは IsPresentationAvailable でも弾く)
            if (IsClient && DeathBodyPresenter.IsPresentationAvailable)
            {
                _deathBody = DeathBodyPresenter.Attach(gameObject);
                _deathBody.Id = "client=" + OwnerClientId.ToString(CultureInfo.InvariantCulture);
            }
        }

        public override void OnNetworkDespawn()
        {
            _team.OnValueChanged -= HandleTeamChanged;
            _health.OnValueChanged -= HandleHealthChanged;
            if (_weapon != null)
            {
                _weapon.ReloadOrSwitchStarted -= HandleReloadOrSwitchStarted;
            }

            ReleaseAttackAction();
            SetLocalView(false);
            DestroyHud();

            _deathBodyPending = false;
            if (_deathBody != null)
            {
                _deathBody.Clear(DeathBodyPresenter.ReasonDestroyed);
            }

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

        private void ReleaseSprint(string reason)
        {
            if (_motor != null)
            {
                _motor.ReleaseSprint(reason);
            }
        }

        /// <summary>
        /// 射撃の始点。目 (<see cref="View"/>) の位置で、立っていれば本体の中心から <see cref="EyeHeight"/> 上、
        /// しゃがんでいれば <see cref="NetworkPlayerMotor"/> が下げた高さになる。
        /// 三人称でもカメラではなく目から撃つ (サーバーは始点を目の高さで検査する)。View が無ければ同じ高さを計算で出す (<see cref="EyePosition"/>)。
        /// </summary>
        public Vector3 AimOrigin()
        {
            EnsureViewResolved();
            return _view != null ? _view.position : EyePosition();
        }

        /// <summary>
        /// 今の姿勢での目のワールド座標を計算で出す。サーバーの射撃の検査 (<see cref="NetTeamDeathmatch"/> の始点の遮蔽判定) が使う。
        /// 姿勢を持たない (<see cref="NetworkPlayerMotor"/> が無い・設定が不正) ときは本体の中心から <see cref="EyeHeight"/> 上。
        /// </summary>
        public Vector3 EyePosition()
        {
            var stance = Stance;
            return stance != null ? stance.EyePosition : transform.position + (Vector3.up * EyeHeight);
        }

        /// <summary>姿勢。同じオブジェクトの <see cref="NetworkPlayerMotor"/> が持つ。無ければ null。</summary>
        public NetStance Stance
        {
            get
            {
                // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で探す
                if (_motor == null)
                {
                    _motor = GetComponent<NetworkPlayerMotor>();
                }

                return _motor != null ? _motor.Stance : null;
            }
        }

        /// <summary>しゃがんでいるか (このインスタンスで今当てている姿勢)。</summary>
        public bool IsCrouched
        {
            get
            {
                var stance = Stance;
                return stance != null && stance.IsCrouched;
            }
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
        /// 視点を有効・無効にする。有効にするとカメラ・AudioListener・<see cref="ViewRig"/>・<see cref="FirstPersonLook"/>・<see cref="AimDownSights"/> を動かし、
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

            if (_ads != null)
            {
                // 無効にすると OnDisable で視野角と感度を腰だめに戻す
                _ads.enabled = enable;
                if (enable && !_ads.enabled)
                {
                    // 参照か Aim アクションを解決できなかった。理由は AimDownSights がエラーに出している
                    Debug.LogWarning("[Net] " + name + ": AimDownSights could not start; aiming down sights is unavailable", this);
                }
            }
            else if (enable)
            {
                Debug.LogWarning("[Net] " + name + " has no AimDownSights; run Fps.Editor.NetSceneBuilder.Build", this);
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

        /// <summary>
        /// プレイヤー本体 root の目。子 <see cref="ViewName"/>、無ければ (カメラが目を兼ねる古い構成) 子のカメラ、どちらも無ければ null。
        /// <see cref="NetStance"/> もこれで探すので、しゃがみで下げる目と射撃の始点は同じ Transform になる。
        /// </summary>
        public static Transform FindView(Transform root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var view = root.Find(ViewName);
            if (view != null)
            {
                return view;
            }

            var camera = FirstPersonLook.FindChildCamera(root);
            return camera != null ? camera.transform : null;
        }

        // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で解決する。プレハブに無いときに毎フレーム探さないよう 1 回だけ
        private void ResolveDeathCamera()
        {
            if (!_deathCameraResolved)
            {
                _deathCameraResolved = true;
                _deathCamera = GetComponent<NetDeathCamera>();
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
            ResolveAds();
            _viewCamera = FirstPersonLook.FindChildCamera(transform);
            _view = FindView(transform);
            _viewListener = _viewCamera != null ? _viewCamera.GetComponent<AudioListener>() : null;
        }

        // ADS は自分の画面の見た目だけで、サーバーには送らない。ダッシュの解除も持ち主の入力の側で行う
        private void ResolveAds()
        {
            if (_ads != null)
            {
                _ads.AimingChanged -= HandleAimingChanged;
            }

            _ads = GetComponent<AimDownSights>();
            if (_ads == null)
            {
                return;
            }

            _ads.WeaponSource = ResolveEquippedSpec;
            _ads.AimingChanged += HandleAimingChanged;
        }

        private void HandleAimingChanged(bool aiming)
        {
            if (aiming && _motor != null)
            {
                ReleaseSprint(SprintInput.ReasonAim);
            }
        }

        // サーバーが確かめたリロード・持ち替えだけで解除する (満タンで断られたリロードでは覗き込みを続ける)
        private void HandleReloadOrSwitchStarted(NetWeaponState state)
        {
            if (_ads != null)
            {
                _ads.Release();
            }
        }

        // しゃがみはオーナーの NetworkPlayerMotor にしか状態が無く、ADS は視点のある持ち主でしか初期化されないので、他では何もしない
        private void ResetStance(string reason)
        {
            if (_motor != null)
            {
                _motor.ReleaseCrouch(reason);
            }

            if (_ads != null && _ads.IsInitialized)
            {
                _ads.ResetView();
            }
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
            // サーバーがテレポートさせるのは出現位置に置くとき (リスポーン) だけ
            ResetStance(CrouchInput.ReasonRespawn);
            ReleaseSprint(SprintInput.ReasonRespawn);
            Teleported?.Invoke(position, rotation);
        }

        private void HandleTeamChanged(Team previous, Team current)
        {
            ApplyVisuals();
        }

        private void HandleHealthChanged(int previous, int current)
        {
            ApplyVisuals();

            // しゃがみ・ダッシュの入力の状態はオーナーの NetworkPlayerMotor にしか無く、他では何もしない
            var wasAlive = previous > 0;
            var isAlive = current > 0;
            if (wasAlive != isAlive)
            {
                ResetStance(isAlive ? CrouchInput.ReasonRespawn : CrouchInput.ReasonDeath);
                ReleaseSprint(isAlive ? SprintInput.ReasonRespawn : SprintInput.ReasonDeath);
            }

            if (_deathBody == null)
            {
                return;
            }

            if (wasAlive && !isAlive)
            {
                // 弾の向きは体力と同じ tick に届くが、ホストではサーバーが体力を書いた後に書くので、ここではまだ古い。
                // LateUpdate まで待てばどちらでも揃っている
                _deathBodyPending = true;
            }
            else if (isAlive)
            {
                _deathBodyPending = false;
                _deathBody.Clear(DeathBodyPresenter.ReasonRespawn);
            }
        }

        private void LateUpdate()
        {
            if (!_deathBodyPending)
            {
                return;
            }

            _deathBodyPending = false;
            if (_deathBody == null || IsAlive)
            {
                return;
            }

            // 試合のルールが届く前か、知らない値が届いたときは今までどおり消すだけにする
            var match = NetTeamDeathmatch.Current;
            var mode = match != null ? match.SyncedRules.DeathBody : DeathBody.Vanish;
            _deathBody.Mode = Enum.IsDefined(typeof(DeathBody), mode) ? mode : DeathBody.Vanish;

            var direction = _lastHitDirection.Value;
            _deathBody.Present(direction.sqrMagnitude > 0f ? direction : (Vector3?)null);
        }

        /// <summary>
        /// サーバーから、このプレイヤーが最後に受けた弾の進む向きを書き込む。倒れたときのやられ演出 (<see cref="DeathBodyPresenter"/>) にだけ使う。
        /// 長さが 0 か有限でない向きは無視する。
        /// </summary>
        internal void ServerSetLastHitDirection(Vector3 direction)
        {
            RequireServer();
            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
            {
                return;
            }

            _lastHitDirection.Value = direction.normalized;
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

        private void RequireServer()
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("only the server may change " + name);
            }
        }
    }
}
