using System;
using System.Collections.Generic;
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
    /// HUD も同じ条件 (<see cref="NetStartup.ShouldCreateHud"/>) で生成時に <see cref="NetUi.CreateHud"/> で作り、消滅時に消す。
    /// 生成役が登録されていなければ (画面のアセンブリ Fps.Net.UI が無ければ) HUD 無しで進める。
    /// </para>
    /// <para>
    /// 倒れると見た目と当たりを消す。試合のルール (<see cref="NetTeamDeathmatch.SyncedRules"/>) のやられ演出が Collapse なら、
    /// 画面のあるクライアントでだけ <see cref="DeathBodyPresenter"/> が抜け殻をサーバーから届いた最後の被弾の向きへ倒し、リスポーンで消す。
    /// 抜け殻はローカルな見た目だけで、判定と同期には関わらない。
    /// </para>
    /// <para>
    /// 射撃の入力と <see cref="FireServerRpc"/> は .Fire.cs に、ダウンと蘇生 (同期・見た目・頭の上の印・Interact と <see cref="ReviveRpc"/>) は
    /// .Downed.cs に、他人の被弾を演出に知らせる <see cref="Hurt"/> は .Hurt.cs に、グレネードの入力と残りの同期は .Grenade.cs に分けてある。ダウン中は撃てない。
    /// </para>
    /// <para>
    /// 見た目はプレハブに置いた人型のリグ (NetSceneBuilder) で、チームの色はリグの材質に塗る。リグは
    /// <see cref="CharacterAnimator"/> が <see cref="ICharacterPoseSource"/> としてのこのプレイヤーから状態を読んで動かす。
    /// 読む値はどれも他のクライアントにも同期されているもの (しゃがみ・接地は <see cref="NetworkPlayerMotor"/>、ダウン・体力・武器の Id)。
    /// ADS だけは持ち主の画面にしか無いので、持ち主が <see cref="IsAiming"/> の同期値に書く。
    /// </para>
    /// <para>
    /// 弾のばらつき (<see cref="SpreadPattern"/>) は、サーバーが数える発射番号 (<see cref="ServerShotSequence"/>) とプレイヤー Id を種に、
    /// ADS の同期値と移動の速さ (<see cref="SpreadMoveFraction"/>) で決める。持ち主も同じ種 (<see cref="NetShotSequence"/>) で予測して曳光弾を出し、
    /// 反動 (<see cref="RecoilState"/>) は持ち主のカメラ (<see cref="FirstPersonLook.Recoil"/>) にだけ効かせる。
    /// </para>
    /// <para>
    /// ブラインドファイア (ルールの <see cref="NetMatchRules.BlindFire"/> が On でカバー中に覗かずに撃つ) は <see cref="NetBlindFire"/> で決める。
    /// 持ち主は予測したカバーの状態から始点と散らばりを予測して曳光弾を出すが、サーバーへは目の始点を送り、判定の始点はサーバーが決める。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class NetPlayer : NetworkBehaviour, ICharacterPoseSource
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

        // 持ち主が覗き込んでいるか。他のクライアントでリグを構えさせ、サーバーが弾のばらつきを決めるのに使う。
        // 覗き込みは持ち主の画面にしか無いので、サーバーを通さず持ち主が書く
        private readonly NetworkVariable<bool> _aiming = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private bool _canPublishAiming;

        private NetworkPlayerMotor _motor;
        private NetPlayerWeapon _weapon;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider[] _colliders = Array.Empty<Collider>();

        // チームの色を塗る、元の材質ごとの複製。リグは部品ごとに Renderer を持つので、renderer.material では部品の数だけ複製ができる
        private Material[] _ownedMaterials = Array.Empty<Material>();
        private bool _teamMaterialsReady;

        // 最後に塗ったときの色の組と自分のチーム。どちらかが変われば RefreshTeamColor が塗り直す
        private ColorPalette _paintedPalette;
        private Team? _paintedViewer;
        private InputAction _attackAction;
        private bool _enabledAttackAction;

        // 装備中の武器の射撃モードと連射間隔。持ち主だけが ResolveTrigger で作る。_triggerWeaponId・_triggerLoadout・_triggerCatalog はどの武器で引いたか
        private FireTrigger _trigger;
        private WeaponSpec _triggerSpec;
        private string _triggerWeaponId;
        private LoadoutDefinition _triggerLoadout;
        private WeaponCatalogAsset _triggerCatalog;
        private string _triggerError;
        private bool _triggerResolved;

        // 発射番号。持ち主は予測に、サーバーは判定に使う (ホストでは両方)。どちらもこのプレイヤーオブジェクトの生成から数える
        private readonly NetShotSequence _shots = new NetShotSequence();
        private readonly ServerShotSequence _serverShots = new ServerShotSequence();

        // 持ち主のカメラにだけ配線する反動
        private readonly RecoilState _recoil = new RecoilState();

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
        private INetHud _hud;

        // やられ演出。画面のあるクライアント (ホストを含む) でだけ付ける。倒れた tick の LateUpdate で出す
        private DeathBodyPresenter _deathBody;
        private bool _deathBodyPending;

        // 武器の演出。プレハブに無ければ null
        private WeaponPresentation _presentation;

        // 映している間は射撃の入力を読まない。プレハブに無ければ null
        private NetDeathCamera _deathCamera;
        private bool _deathCameraResolved;

        public Team Team => _team.Value;
        public int Health => _health.Value;
        public bool IsAlive => _health.Value > 0;

        /// <summary>HUD (Fps.Net.UI の NetHud) が OnValueChanged を購読するチームの同期値。</summary>
        internal NetworkVariable<Team> TeamVariable => _team;

        /// <summary>HUD (Fps.Net.UI の NetHud) が OnValueChanged を購読する体力の同期値。</summary>
        internal NetworkVariable<int> HealthVariable => _health;

        /// <summary>このプレイヤーの HUD。自分のプレイヤーで画面があり、HUD の生成役が登録されているときだけあり、他では null。</summary>
        public INetHud LocalHud => _hud;

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

        /// <summary>目の下のカメラ。プレハブに無ければ null。映しているのは <see cref="HasLocalView"/> のときだけ。</summary>
        internal Camera ViewCamera
        {
            get
            {
                EnsureViewResolved();
                return _viewCamera;
            }
        }

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
            SpawnBotMark();
            _team.OnValueChanged += HandleTeamChanged;
            _health.OnValueChanged += HandleHealthChanged;
            ApplyVisuals();

            // Bot はサーバーが持ち主なので、IsOwner ではなく人が操作しているかで視点・入力・HUD を付ける
            var local = IsLocallyControlled;
            SetLocalView(NetStartup.ShouldEnableFirstPersonView(local, Application.isBatchMode, NetStartup.IsDedicatedServerBuild));

            // NetBootstrap が止めるのは起動時のシーンだけなので、後から生成されるプレイヤーの演出はここで止める
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                HeadlessPresentation.DisableUnder(gameObject);
            }

            // 自分の銃声とリロード音は 2D、他人の銃声は本体の位置から 3D で鳴らす
            _presentation = GetComponent<WeaponPresentation>();
            if (_presentation != null)
            {
                _presentation.IsLocal = local;
            }

            if (local)
            {
                ResolveAttackAction();
                ResolveGrenadeAction();
                ResolvePingAction();
                AttachPingDisplay();
                if (_weapon != null)
                {
                    _weapon.ReloadOrSwitchStarted += HandleReloadOrSwitchStarted;
                    _weapon.ReloadStarted += HandleReloadStarted;
                    _weapon.DryFired += HandleDryFired;
                }

                _canPublishAiming = true;
                PublishAiming(_ads != null && _ads.IsAiming);
            }

            if (NetStartup.ShouldCreateHud(local, Application.isBatchMode, NetStartup.IsDedicatedServerBuild))
            {
                CreateHud();
            }

            // サーバーだけのインスタンスには見る人がいないので作らない (専用サーバーは IsPresentationAvailable でも弾く)
            if (IsClient && DeathBodyPresenter.IsPresentationAvailable)
            {
                _deathBody = DeathBodyPresenter.Attach(gameObject);
                _deathBody.Id = (IsBot ? "bot=" : "client=") + RosterId.ToString(CultureInfo.InvariantCulture);
            }

            SpawnDowned();
        }

        public override void OnNetworkDespawn()
        {
            // 下の SetLocalView(false) で ADS が解除されて AimingChanged が来るが、消滅中には同期値を書かない
            _canPublishAiming = false;
            _team.OnValueChanged -= HandleTeamChanged;
            _health.OnValueChanged -= HandleHealthChanged;
            if (_weapon != null)
            {
                _weapon.ReloadOrSwitchStarted -= HandleReloadOrSwitchStarted;
                _weapon.ReloadStarted -= HandleReloadStarted;
                _weapon.DryFired -= HandleDryFired;
            }

            ReleaseAttackAction();
            ReleaseGrenadeAction();
            ReleasePingAction();
            DetachPingDisplay();
            DespawnDowned();
            ResetSpawnProtectionLook();
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

            // 消滅の通知を経ずに破棄された場合も、一覧と印を残さない
            _spawnedPlayers.Remove(this);
            if (Local == this)
            {
                Local = null;
            }

            DestroyDownedMarker();

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
                var motor = ResolveMotor();
                return motor != null ? motor.Stance : null;
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
                _recoil.Reset();
                _look.Recoil = enable ? _recoil : null;

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

            PublishAiming(aiming);
        }

        private void PublishAiming(bool aiming)
        {
            if (!_canPublishAiming || !IsSpawned || !IsLocallyControlled || _aiming.Value == aiming)
            {
                return;
            }

            _aiming.Value = aiming;
        }

        /// <summary>
        /// 覗き込んで構えているか。持ち主は自分の <see cref="AimDownSights"/> の今の値、他では持ち主が書いた同期値。
        /// リグの姿勢 (<see cref="CharacterAnimator"/>) に使う。弾のばらつきは持ち主でも同期値 (<see cref="AimingForSpread"/>) を読む。
        /// </summary>
        public bool IsAiming
        {
            get
            {
                EnsureViewResolved();
                if (IsSpawned && IsLocallyControlled && _ads != null && _ads.IsInitialized)
                {
                    return _ads.IsAiming;
                }

                return _aiming.Value;
            }
        }

        /// <summary>歩きの水平速度 (m/s)。<see cref="NetworkPlayerMotor"/> が無ければ <see cref="PlayerController.DefaultWalkSpeed"/>。</summary>
        public float WalkSpeed
        {
            get
            {
                var motor = ResolveMotor();
                return motor != null ? motor.WalkSpeed : PlayerController.DefaultWalkSpeed;
            }
        }

        /// <summary>ダッシュ中の水平速度 (m/s)。<see cref="NetworkPlayerMotor"/> が無ければ <see cref="PlayerController.DefaultSprintSpeed"/>。</summary>
        public float SprintSpeed
        {
            get
            {
                var motor = ResolveMotor();
                return motor != null ? motor.SprintSpeed : PlayerController.DefaultSprintSpeed;
            }
        }

        /// <summary>しゃがみ中の水平速度 (m/s)。<see cref="NetworkPlayerMotor"/> が無ければ <see cref="PlayerController.DefaultCrouchSpeed"/>。</summary>
        public float CrouchSpeed
        {
            get
            {
                var motor = ResolveMotor();
                return motor != null ? motor.CrouchSpeed : PlayerController.DefaultCrouchSpeed;
            }
        }

        /// <summary>
        /// リグの姿勢を決める今の状態 (<see cref="ICharacterPoseSource"/>)。しゃがみ・接地はこのインスタンスで当てている値、
        /// ダウン・体力・武器の Id・ADS は同期値なので、どのクライアントでも同じ姿勢になる。
        /// </summary>
        /// <param name="horizontalSpeed">位置の変化から求めた水平の速さ (m/s)。0 以上の有限値。</param>
        public CharacterPoseInput ReadPose(float horizontalSpeed)
        {
            var motor = ResolveMotor();
            var spec = ResolveEquippedSpec();
            return PoseInput(
                horizontalSpeed,
                WalkSpeed,
                motor == null || motor.IsGrounded,
                IsCrouched,
                IsAiming,
                motor != null ? motor.PoseCoverHeight : null,
                IsDowned,
                IsAlive,
                spec != null ? spec.Category : (WeaponCategory?)null);
        }

        /// <summary>
        /// 同期された値からリグの姿勢の入力を作る。倒れている (体力 0) なら死亡として渡す。
        /// ダッシュしているかは他人に同期されないので、立っていて歩きより速く動いていればダッシュとみなす
        /// (<see cref="NetworkPlayerMotor"/> で歩きより速く動けるのはダッシュだけ)。
        /// </summary>
        /// <param name="coverHeight">張り付いている壁の高さ (<see cref="NetworkPlayerMotor.PoseCoverHeight"/>)。張り付いていなければ null。</param>
        internal static CharacterPoseInput PoseInput(
            float horizontalSpeed,
            float walkSpeed,
            bool grounded,
            bool crouched,
            bool aiming,
            CoverHeight? coverHeight,
            bool downed,
            bool alive,
            WeaponCategory? weapon)
        {
            var sprinting = !crouched && horizontalSpeed > walkSpeed;
            return new CharacterPoseInput(horizontalSpeed, grounded, crouched, sprinting, aiming, coverHeight, downed, !alive, weapon);
        }

        /// <summary>
        /// このインスタンスの最新のカバーの状態 (<see cref="NetworkPlayerMotor.Cover"/>)。サーバーでは確定状態、持ち主では最新の予測。
        /// 動きを持たなければカバーの外。ブラインドファイアの始点に使う。
        /// </summary>
        internal NetCoverState CurrentCover
        {
            get
            {
                var motor = ResolveMotor();
                return motor != null ? motor.Cover : NetCoverState.Out;
            }
        }

        /// <summary>体の半径 (m)。動きを持たなければ 0。</summary>
        internal float BodyRadius
        {
            get
            {
                var motor = ResolveMotor();
                return motor != null ? motor.BodyRadius : 0f;
            }
        }

        private NetworkPlayerMotor ResolveMotor()
        {
            // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で探す
            if (_motor == null)
            {
                _motor = GetComponent<NetworkPlayerMotor>();
            }

            return _motor;
        }

        // サーバーが確かめたリロード・持ち替えだけで解除する (満タンで断られたリロードでは覗き込みを続ける)
        private void HandleReloadOrSwitchStarted(NetWeaponState state)
        {
            if (_ads != null)
            {
                _ads.Release();
            }
        }

        // 専用サーバーでは HeadlessPresentation が WeaponPresentation を止めているので、enabled を見て鳴らさない
        private void HandleReloadStarted(string weaponId)
        {
            if (_presentation != null && _presentation.isActiveAndEnabled)
            {
                _presentation.PlayReload(weaponId);
            }
        }

        // 専用サーバーでは DryFireSound が自分を止めているので、enabled を見て鳴らさない
        private void HandleDryFired(string weaponId)
        {
            var sound = GetComponent<DryFireSound>();
            if (sound != null && sound.isActiveAndEnabled)
            {
                sound.Play(weaponId);
            }
        }

        // しゃがみはオーナーの NetworkPlayerMotor にしか状態が無く、ADS は視点のある持ち主でしか初期化されないので、他では何もしない
        private void ResetStance(string reason)
        {
            _recoil.Reset();
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
            ReportHurt(previous, current);

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
            RefreshDownedMarker();
            RefreshSpawnProtection();
            RefreshTeamColor();
            RefreshPingDisplay();

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

        /// <summary>
        /// このプロセスで team のプレイヤーを塗る色。自分のプレイヤー (<see cref="Local"/>) のチームから見た敵味方の色を、
        /// 設定の色の組 (<see cref="TeamColors"/>) から引く。自分のプレイヤーがいなければ Alpha を味方の色にする。
        /// </summary>
        public static Color TeamColor(Team team)
        {
            return TeamColors.Of(team, LocalTeam);
        }

        /// <summary>このプロセスの自分のプレイヤーのチーム。自分のプレイヤーがいなければ null。</summary>
        public static Team? LocalTeam => Local != null ? Local.Team : (Team?)null;

        /// <summary>
        /// 色の組か自分のチームが、最後に塗ったときから変わっていれば塗り直す。自分のプレイヤーのチームは他のプレイヤーの同期値なので、
        /// 購読せずに毎フレーム比べる。
        /// </summary>
        private void RefreshTeamColor()
        {
            if (!_teamMaterialsReady || _ownedMaterials.Length == 0)
            {
                return;
            }

            var palette = AppliedControlSettings.Current.Palette;
            var viewer = LocalTeam;
            if (palette != _paintedPalette || viewer != _paintedViewer)
            {
                ApplyVisuals();
            }
        }

        /// <summary>
        /// チームの色 (<see cref="TeamColor"/>) を見た目 (リグ) の材質に塗り、倒れている間は見た目と当たりを消す。
        /// 出現直後の無敵を見せている間 (<see cref="ShowsSpawnProtection"/>) は、材質を半透明にして色のアルファを <see cref="SpawnProtectionAlpha"/> にする。
        /// </summary>
        private void ApplyVisuals()
        {
            var alive = IsAlive;
            var translucent = _showsSpawnProtection;
            _paintedPalette = AppliedControlSettings.Current.Palette;
            _paintedViewer = LocalTeam;
            var color = TeamColors.Of(TeamPalette.For(_paintedPalette), _team.Value, _paintedViewer);
            color.a = translucent ? SpawnProtectionAlpha : 1f;
            EnsureTeamMaterials();

            foreach (var bodyRenderer in _renderers)
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.enabled = alive;
                }
            }

            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    material.color = color;
                    if (translucent != _materialsTranslucent)
                    {
                        SetTranslucent(material, translucent);
                    }
                }
            }

            // 無敵を一度も見せない (SpawnProtectionSeconds が 0 の) 試合では、複製した材質の描画の設定に触らない
            _materialsTranslucent = translucent;

            foreach (var hitbox in _colliders)
            {
                hitbox.enabled = alive;
            }
        }

        // 複製はこのプレイヤーだけのもので、OnDestroy で破棄する
        private void EnsureTeamMaterials()
        {
            if (_teamMaterialsReady)
            {
                return;
            }

            _teamMaterialsReady = true;
            _ownedMaterials = ReplaceWithCopies(_renderers);
        }

        /// <summary>
        /// renderers の材質を、元の材質ごとに 1 つずつ作った複製へ差し替え、作った複製を返す。
        /// 同じ元の材質の部品は同じ複製を共有する。元の材質 (リグのプレハブの共有の材質) は変えない。null の Renderer と材質は飛ばす。
        /// 複製の破棄は呼んだ側が行う。
        /// </summary>
        internal static Material[] ReplaceWithCopies(IReadOnlyList<Renderer> renderers)
        {
            if (renderers == null)
            {
                throw new ArgumentNullException(nameof(renderers));
            }

            var copies = new Dictionary<Material, Material>();
            var owned = new List<Material>();
            foreach (var bodyRenderer in renderers)
            {
                if (bodyRenderer == null)
                {
                    continue;
                }

                var materials = bodyRenderer.sharedMaterials;
                var replaced = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null)
                    {
                        continue;
                    }

                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source) { name = source.name + " (Team)" };
                        copies.Add(source, copy);
                        owned.Add(copy);
                    }

                    materials[i] = copy;
                    replaced = true;
                }

                if (replaced)
                {
                    bodyRenderer.sharedMaterials = materials;
                }
            }

            return owned.ToArray();
        }

        private void CreateHud()
        {
            if (GameUi.IsAlive(_hud))
            {
                return;
            }

            try
            {
                _hud = NetUi.CreateHud(this);
                if (_hud == null)
                {
                    Debug.Log(NetUi.NoHudLog, this);
                    return;
                }

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
            if (GameUi.IsAlive(_hud))
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
