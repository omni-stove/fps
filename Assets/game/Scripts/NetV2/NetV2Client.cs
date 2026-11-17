using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.NetV2
{
    /// <summary>
    /// ゲームのネット対戦のクライアント。Shooter.Protocol と LiteNetLib (<see cref="UdpMatchTransport"/>) か、
    /// <c>-transport eos</c> なら EOS の P2P (<see cref="NetV2Eos"/>) で .NET のサーバーへつなぎ、
    /// 承認されたらサーバーのマップのシーンへ移って、自分のプレイヤー (<see cref="NetV2LocalPlayer"/>)・他のプレイヤー
    /// (<see cref="NetV2RemotePlayer"/>)・HUD に出す値 (<see cref="NetV2Hud"/>) を作る。HUD は UI がその値を見て作る。シーンをまたいで残る。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 自分の移動は Core の移動で予測して入力を送り、スナップショットで照合する。他のプレイヤーは補間して出す (<see cref="ClientMatch"/>)。
    /// 当たりはサーバーと同じマップの <c>.fpsmap</c> と <c>.ladders.json</c> (<see cref="ClientWorld"/>) で取り、壊れた建物の部品は
    /// サーバーの知らせで外す (<see cref="NetV2Structures"/>)。移動の値はサーバーが承認のときに送る、サーバーのルールのファイルの値
    /// (<see cref="MatchClient.Motor"/>) を使う。はしごにつかまったら視点をはしごへ向ける。
    /// </para>
    /// <para>
    /// 射撃は今の武器の撃ち方 (<see cref="NetV2Weapons"/> の <see cref="ClientTrigger"/>) で、リロード・弾薬の切り替え・グレネード・印は押したときに要求を送る。
    /// 飛ぶ弾・ビーム・火炎・状態異常・範囲の効果の知らせとスナップショットは、機能ごとの受け口 (<see cref="ClientWeaponHooks"/>) に渡す。
    /// 承認されたら保存してあるロードアウトを頼み、倒れている間はロードアウトを選ぶ画面を出す (<see cref="NetV2LoadoutChooser"/>)。
    /// サーバーのイベントは、オフラインと Netcode のクライアントと同じ <see cref="CombatEvents"/> と HUD に流し、<c>[Combat]</c> の行を出す。
    /// 印は Netcode のクライアントと同じ <see cref="PingController"/> に、拠点の持ち主はマップの <see cref="ControlPointZone"/> に出す。
    /// </para>
    /// <para>
    /// 起動引数は <see cref="NetClientLaunch"/> が読む。<c>-mode client</c> なら起動してすぐ <c>-address</c> か <c>-eos-server</c> のサーバーへ、
    /// <c>-lobby quickmatch</c> なら起動してすぐ専用サーバーのロビーを探して (<see cref="QuickMatchSearch"/>) つなぐ。
    /// メニューのクイックマッチ (<see cref="GameUi.OnlineStarter"/>) は、<c>-address</c>・<c>-port</c>・<c>-eos-server</c> があればそのサーバーへ、
    /// 無ければロビーを探してつなぐ。EOS のロビーが使えない (EOS がビルドに無い・認証情報が揃わない) UDP のときは、
    /// <see cref="NetClientLaunch.DefaultServer"/> へつなぐ。
    /// </para>
    /// <para>
    /// サーバーとの接続を失ったら (つながらない・断られたを含む)、Netcode のクライアントと同じ理由 (<see cref="DisconnectNotice"/>) を
    /// <see cref="MenuReturnReason"/> に入れてメニューへ戻る。
    /// batchmode のプレイヤーでは Netcode のクライアントと同じ終了コードで終わる。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetV2Client : MonoBehaviour
    {
        public const string ObjectName = "NetV2Client";
        public const string LogTag = "[Net]";
        public const string CombatLogTag = "[Combat]";

        // Netcode のクライアント (NetBootstrap) と同じ終了コード
        public const int ExitDisconnected = 0;
        public const int ExitInvalidArguments = NetClientLaunch.ExitInvalidArguments;
        public const int ExitMisconfigured = 3;
        public const int ExitConnectFailed = 5;
        public const int ExitTransportFailure = 6;
        public const int ExitLobbyFailed = 7;

        private const string _sceneExtension = ".unity";

        /// <summary>このプロセスの端末の Id。Netcode のクライアントと同じく、同じプロセスのままつなぎ直したときだけ成績を引き継ぐ。</summary>
        private static readonly string _deviceKey = RejoinKey.NewDeviceKey();

        private readonly Dictionary<int, NetV2RemotePlayer> _remotes = new Dictionary<int, NetV2RemotePlayer>();
        private readonly FireRequestMessage _fire = new FireRequestMessage();
        private readonly ThrowGrenadeRequestMessage _grenade = new ThrowGrenadeRequestMessage();
        private readonly PingRequestMessage _ping = new PingRequestMessage();
        private readonly WeaponActionRequestMessage _weaponAction = new WeaponActionRequestMessage();
        private readonly List<ControlPointZone> _zones = new List<ControlPointZone>();
        private readonly NetV2Weapons _weapons = new NetV2Weapons();
        private readonly ClientGrenades _grenadeChoice = new ClientGrenades();
        private readonly Dictionary<int, AreaEffectZone> _areaZones = new Dictionary<int, AreaEffectZone>();
        private readonly Dictionary<int, GameObject> _grenadeViews = new Dictionary<int, GameObject>();
        private readonly List<int> _goneGrenades = new List<int>();
        private GrenadeCarrier _grenadeCarrier;
        private float _ownYaw;
        private NetV2Structures _structures;
        private NetV2Vehicles _vehicles;
        private ClientWeaponHooks _weaponHooks;
        private ClientTrigger _heldContinuous;
        private string _heldContinuousWeaponId;
        private NetV2LoadoutChooser _loadout;
        private NetV2Inventory _inventory;
        private ClientPings _pings;
        private NetLaunchOptions _options;
        private MatchClient _client;
        private ClientAntiCheat _antiCheat;
        private ClientMatch _match;
        private QuickMatchSearch _search;
        private ClientWorld _world;
        private NetV2LocalPlayer _local;
        private WeaponPresentation _presentation;
        private DryFireSound _dryFire;
        private LockOnAim _lockOn;
        private PingController _pingDisplay;
        private bool _wasReloading;
        private bool _startedFromMenu;
        private NetV2Hud _hud;
        private Transform _remoteRoot;
        private IMenuNavigator _menu;
        private AsyncOperation _mapLoad;
        private int _shotIndex;
        private int _shotGeneration;
        private bool _finished;

        public MatchClient Client => _client;

        public ClientMatch Match => _match;

        public GameObject GrenadePrefab { get; set; }

        /// <summary>今動いているクライアント。無ければ null。</summary>
        public static NetV2Client Current { get; private set; }

        /// <summary>
        /// 最初のシーンを読んだ後に起動引数を読み (<see cref="NetClientLaunch.ResolveStart"/>)、メニューのクイックマッチをこのクライアントにする。
        /// <c>-mode client</c> か <c>-lobby quickmatch</c> ならすぐつなぐ。引数が誤っていれば誤りを出して終了コード 2 で終える。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var start = NetClientLaunch.ResolveStart(Environment.GetCommandLineArgs(), out var server, out var error);
            if (start == NetClientStart.Invalid)
            {
                Debug.LogError(LogTag + " invalid arguments: " + error);
                if (!Application.isEditor)
                {
                    Application.Quit(ExitInvalidArguments);
                }

                return;
            }

            GameUi.OnlineStarter = new MenuStarter(server);
            switch (start)
            {
                case NetClientStart.Connect:
                    Begin(server, false);
                    break;
                case NetClientStart.QuickMatch:
                    BeginQuickMatch(false);
                    break;
                default:
                    Debug.Log(LogTag + " the menu's quick match " + (server != null ? "connects to " + server.Endpoint : "searches the server lobbies"));
                    break;
            }
        }

        /// <summary>options のサーバーへつなぐクライアントを作る。シーンをまたいで残る。</summary>
        public static NetV2Client Begin(NetLaunchOptions options, bool fromMenu)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            var client = host.AddComponent<NetV2Client>();
            client._startedFromMenu = fromMenu;
            client.Connect(options);
            return client;
        }

        /// <summary>
        /// 専用サーバーのロビーを探し (<see cref="QuickMatchSearch"/>)、見つけたサーバーへつなぐクライアントを作る。シーンをまたいで残る。
        /// </summary>
        public static NetV2Client BeginQuickMatch(bool fromMenu)
        {
            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            var client = host.AddComponent<NetV2Client>();
            client._startedFromMenu = fromMenu;
            client.SearchServer();
            return client;
        }

        /// <summary>
        /// EOS のロビーを探せるか。EOS がビルドにあり、認証情報が揃っていれば (読めないファイルは誤りを出すため探す側に回す) true。
        /// </summary>
        private static bool CanSearchLobbies()
        {
            var factory = NetV2Eos.Factory;
            if (factory == null)
            {
                return false;
            }

            return !factory.TryLoadCredentials(out var credentials, out _, out _) || credentials != null;
        }

        private void SearchServer()
        {
            var factory = NetV2Eos.Factory;
            if (factory == null)
            {
                Leave(ExitMisconfigured, QuickMatchSearch.LogPrefix
                    + "the EOS lobby is not in this build (is com.playeveryware.eos installed and Shooter.NetEos compiled?)");
                return;
            }

            if (!factory.TryLoadCredentials(out var credentials, out var missing, out var loadError))
            {
                Leave(ExitMisconfigured, QuickMatchSearch.LogPrefix + "cannot load EOS credentials: " + loadError);
                return;
            }

            if (credentials == null)
            {
                Leave(ExitMisconfigured, QuickMatchSearch.LogPrefix + "quick match needs EOS credentials; missing " + string.Join(", ", missing));
                return;
            }

            var backend = factory.CreateLobbyBackend(credentials);
            if (backend == null)
            {
                Leave(ExitMisconfigured, QuickMatchSearch.LogPrefix + "the EOS lobby factory returned no backend");
                return;
            }

            _search = new QuickMatchSearch(backend, Application.version, line => Debug.Log(line, this));
            _search.Begin(Time.unscaledTime, HandleServerFound, HandleSearchFailed);
        }

        private void HandleServerFound(NetLaunchOptions options)
        {
            if (_finished)
            {
                return;
            }

            Connect(options);
        }

        private void HandleSearchFailed(string reason)
        {
            Leave(ExitLobbyFailed, QuickMatchSearch.LogPrefix + reason);
        }

        private void Awake()
        {
            Current = this;

            // サーバーと同じマシンで並べて動かすので、裏に回っても止めない
            Application.runInBackground = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.targetFrameRate = 60;
            }

            _menu = GameUi.Menu;
            if (_menu != null)
            {
                _menu.ReturningToMenu += HandleReturningToMenu;
            }

            _structures = new NetV2Structures(this);
            _vehicles = new NetV2Vehicles(this);
            _weaponHooks = new ClientWeaponHooks(_weapons.SpecFor);
            _weaponHooks.StatusEffects.Started += ShowStatusEffect;
            _weaponHooks.StatusEffects.Ended += HideStatusEffect;
            _weaponHooks.AreaEffects.Started += ShowArea;
            _weaponHooks.AreaEffects.Updated += UpdateArea;
            _weaponHooks.AreaEffects.Ended += EndArea;
            _weaponHooks.Beams.Drawn += DrawBeam;
            _weaponHooks.Beams.Hidden += HideBeam;
            _weaponHooks.Spray.Drawn += DrawSpray;
            _weaponHooks.Spray.Hidden += HideSpray;
            _pings = new ClientPings(line => Debug.Log(line, this));
            _loadout = new NetV2LoadoutChooser(_weapons, SendReliable, this);
            _inventory = new NetV2Inventory(SendReliable);
            _weapons.OwnAttachments = weaponId => _inventory.Inventory?.AttachmentsOf(weaponId);
        }

        private void Connect(NetLaunchOptions requested)
        {
            _options = requested;
            if (!BuildVersion.TryNormalize(Application.version, out var version, out var versionError))
            {
                Leave(ExitMisconfigured, LogTag + " cannot read the build version (Application.version): " + versionError);
                return;
            }

            if (!TryPlanTransport(requested, out var plan, out var planError))
            {
                Leave(ExitMisconfigured, planError);
                return;
            }

            _options = plan.Options;
            if (!AntiCheatSettings.TryResolve(Environment.GetCommandLineArgs(), Application.isEditor, _options, out var antiCheat, out var antiCheatError))
            {
                Leave(ExitInvalidArguments, AntiCheatSettings.LogTag + " invalid arguments: " + antiCheatError);
                return;
            }

            Debug.Log(antiCheat.Describe(), this);
            var eos = _options.Transport == NetTransportKind.Eos;
            var transport = eos ? NetV2Eos.Factory.Create(plan.Credentials, line => Debug.Log(line, this)) : new UdpMatchTransport();
            _client = new MatchClient(transport, version, _deviceKey, "", line => Debug.Log(line, this));
            _client.Accepted += HandleAccepted;
            _client.SnapshotReceived += HandleSnapshot;
            _client.MessageReceived += HandleMessage;
            _client.Disconnected += HandleDisconnected;
            if (!NetV2AntiCheat.TryCreate(antiCheat, _client, HandleAntiCheatRemoved, out _antiCheat, out antiCheatError))
            {
                Leave(ExitMisconfigured, AntiCheatSettings.LogTag + " " + antiCheatError);
                return;
            }

            Debug.Log(LogTag + " starting " + _options + " transport=" + NetLaunchOptions.TransportName(_options.Transport), this);
            try
            {
                _client.Connect(eos ? _options.EosServerId : _options.Address, _options.Port);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Leave(ExitConnectFailed, LogTag + " failed to start " + _options + ": " + e.Message);
            }
        }

        /// <summary>
        /// Netcode のクライアントと同じく、EOS を求められても認証情報が揃わなければ UDP でつなぐ。
        /// 認証情報のファイルが壊れているときは、黙って UDP にすると誤りに気づけないので止める。
        /// </summary>
        private bool TryPlanTransport(NetLaunchOptions requested, out NetTransportPlan plan, out string error)
        {
            plan = null;
            EosCredentials credentials = null;
            IReadOnlyList<string> missing = null;
            if (requested.Transport == NetTransportKind.Eos)
            {
                var factory = NetV2Eos.Factory;
                if (factory == null)
                {
                    error = LogTag + " the EOS transport is not in this build (is com.playeveryware.eos installed and Shooter.NetEos compiled?)";
                    return false;
                }

                if (!factory.TryLoadCredentials(out credentials, out missing, out var loadError))
                {
                    error = LogTag + " cannot load EOS credentials: " + loadError;
                    return false;
                }
            }

            plan = NetTransportPlan.Decide(requested, credentials, missing);
            if (plan.FellBackToUdp)
            {
                Debug.LogWarning(NetTransportPlan.CredentialsMissingLog, this);
                Debug.Log(plan.DescribeMissingCredentials(), this);
            }

            error = null;
            return true;
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            _search?.Tick(Time.unscaledTime);
            if (_client == null || _finished)
            {
                return;
            }

            _client.Poll();
            if (_finished)
            {
                return;
            }

            _antiCheat.Tick(Time.realtimeSinceStartupAsDouble);
            if (_finished)
            {
                return;
            }

            if (_mapLoad != null && _mapLoad.isDone)
            {
                _mapLoad = null;
                EnterMap();
            }

            if (_match == null)
            {
                return;
            }

            StepOwner(Time.deltaTime);
            _weaponHooks.StatusEffects.Tick(Time.deltaTime);
            ShowRemotes();
            ShowGrenades();
            _weaponHooks.Projectiles.Tick(Time.deltaTime);
            SendRequests();
            if (_local != null)
            {
                _weaponHooks.Beams.Update(NetV2Math.ToNumerics(_local.EyePosition), NetV2Math.ToNumerics(_local.AimDirection));
                _weaponHooks.Spray.Update(NetV2Math.ToNumerics(_local.EyePosition), NetV2Math.ToNumerics(_local.AimDirection));
            }

            _pings.Markers.Expire(Time.time);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            _loadout?.Dispose();
            _search?.Dispose();
            if (_menu != null)
            {
                _menu.ReturningToMenu -= HandleReturningToMenu;
                _menu = null;
            }

            _match?.Detach();
            _antiCheat?.End();
            _client?.Dispose();
            DestroyViews();
        }

        private void HandleAccepted()
        {
            if (!_antiCheat.ServerConnected(Time.realtimeSinceStartupAsDouble, out var antiCheatError))
            {
                Leave(ExitTransportFailure, AntiCheatSettings.LogTag + " " + antiCheatError + NetV2AntiCheat.LaunchHint);
                return;
            }

            _loadout.SendSaved();
            var map = _client.Map;
            if (SceneManager.GetActiveScene().name == map)
            {
                EnterMap();
                return;
            }

            if (!TryFindScene(map, out var path))
            {
                Leave(ExitMisconfigured, LogTag + " the server's map " + map + " is not a scene in this build; build the map and rebuild");
                return;
            }

            Debug.Log(LogTag + " loading the server's map " + map + " (" + path + ")", this);
            _mapLoad = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        private void EnterMap()
        {
            var map = _client.Map;
            if (!ClientWorld.TryLoad(MapCollisionFiles.Directory, map, out _world, out var error))
            {
                Leave(ExitMisconfigured, LogTag + " " + error);
                return;
            }

            Debug.Log(_world.LogLine, this);
            var motor = _client.Motor;
            Debug.Log(LogTag + " motor from the server's rules walk=" + motor.Motor.WalkSpeed.ToString(CultureInfo.InvariantCulture)
                + " sprint=" + motor.Motor.SprintSpeed.ToString(CultureInfo.InvariantCulture)
                + " height=" + motor.Stance.Standing.Height.ToString(CultureInfo.InvariantCulture)
                + " radius=" + motor.Stance.Radius.ToString(CultureInfo.InvariantCulture)
                + " proneSpeed=" + motor.Prone.Speed.ToString(CultureInfo.InvariantCulture), this);
            _match = new ClientMatch(_client, _world.CreateSimulation(motor, _client.TickRate, _client.KillPlaneY, _client.Knockback));
            _weaponHooks.Projectiles.Attach(
                new NetV2Projectiles(_weapons), _world.Collision, Physics.DefaultRaycastLayers, NetV2Math.ToNumerics(Physics.gravity), _client.PlayerId);
            _weaponHooks.Beams.Attach(_world.Collision, Physics.DefaultRaycastLayers, _client.PlayerId);
            _weaponHooks.Spray.Attach(_world.Collision, Physics.DefaultRaycastLayers, _client.PlayerId);
            _match.PlayerAdded += AddRemote;
            _match.PlayerRemoved += RemoveRemote;
            _match.Reconciled += HandleReconciled;
            _match.LadderGrabbed += HandleLadderGrabbed;
            var scene = SceneManager.GetActiveScene();
            _structures.Enter(scene, _world);
            _vehicles.Enter(scene, _match.Vehicles, _client.Motor.Stance, _weapons);
            CollectControlPoints(scene);
            _remoteRoot = new GameObject("NetV2Players").transform;
            foreach (var id in _match.RemotePlayerIds)
            {
                AddRemote(id);
            }

            _hud = NetV2Hud.Create(_client.PlayerId, _client.MaxHealth, _weapons.OwnSpecFor);
            _hud.BindStatusEffects(_weaponHooks.StatusEffects);
            _hud.BindInventory(_inventory);

            _loadout.Bind(GameUi.LoadoutSelect);

            if (_client.LatestSnapshot != null)
            {
                HandleSnapshot(_client.LatestSnapshot);
            }

            Debug.Log(LogTag + " synchronized with the server; map " + map, this);
        }

        private void StepOwner(float deltaSeconds)
        {
            var ticks = _match.Advance(deltaSeconds);
            if (!_match.IsPredicting)
            {
                return;
            }

            if (_local == null)
            {
                var start = _match.LocalState;
                _local = NetV2LocalPlayer.Create(
                    _client.Motor.Stance, _match.Prediction.Simulation.Body.HasHeadroomToStand, NetV2Math.ToUnity(start.Position), OwnYaw());
                _presentation = _local.GetComponent<WeaponPresentation>();
                _dryFire = _local.GetComponent<DryFireSound>();

                // 印を見る画面があるのは自分のプレイヤーだけなので、表示もここにだけ付ける (Netcode の NetPlayer と同じ)
                _pingDisplay = _local.gameObject.AddComponent<PingController>();
                _pingDisplay.ShowShared(_pings.Markers);
                _lockOn = AttachLockOn(_local);
            }

            for (var i = 0; i < ticks; i++)
            {
                _local.Sample(_match.LocalState.IsGrounded, out var move, out var yaw, out var buttons);
                _ownYaw = yaw;
                _match.Step(NetV2Math.ToNumerics(move), yaw, buttons);
            }

            _match.SendInput();
            _vehicles.Show(_match.Interpolation.RenderTick, _local);
            if (ticks > 0)
            {
                _vehicles.SendMove(_client);
            }

            // 座席にいる間の位置は乗り物の動きで決まるので、予測ではなく見せている乗り物の座席に置く
            if (_vehicles.TrySeatOrigin(out var seated, out var crouched))
            {
                _local.ShowSeated(seated, crouched);
            }
            else
            {
                _local.Show(_match.LocalState);
            }
        }

        // 誘導の向きを決めるのはサーバーなので、ここでは相手を捉えて HUD と音に出し、撃つときにロックした相手の Id を送るだけにする。
        private LockOnAim AttachLockOn(NetV2LocalPlayer local)
        {
            var lockOn = local.gameObject.AddComponent<LockOnAim>();
            lockOn.RoundSource = () => ClientFire.CurrentRound(CurrentOwner(), _weapons.OwnSpecFor(ClientFire.CurrentWeaponId(CurrentOwner())));
            lockOn.LookSource = () =>
            {
                var definition = _weapons.DefinitionFor(ClientFire.CurrentWeaponId(CurrentOwner()));
                return definition != null ? definition.Homing : null;
            };
            lockOn.AimingSource = () => local != null && local.IsAiming;
            lockOn.AimSource = () => new Ray(local.EyePosition, local.AimDirection);
            lockOn.Targets = new ClientLockOnTargets(
                _client.PlayerId, () => _client.LatestSnapshot, _match.TrySampleRemote, _world.Collision, _client.Motor.Stance);
            return lockOn;
        }

        private OwnerSnapshot CurrentOwner()
        {
            var latest = _client.LatestSnapshot;
            return latest != null && latest.HasOwner ? latest.Owner : null;
        }

        private float OwnYaw()
        {
            var latest = _client.LatestSnapshot;
            var own = latest != null ? latest.Players.IndexOf(_client.PlayerId) : -1;
            return own >= 0 ? latest.Players[own].Yaw : 0f;
        }

        private void ShowRemotes()
        {
            foreach (var pair in _remotes)
            {
                if (_match.TrySampleRemote(pair.Key, out var pose))
                {
                    pair.Value.Show(pose);
                }
            }
        }

        private void ShowGrenades()
        {
            _grenadeCarrier ??= ShownCarrier;
            var renderTick = _match.Interpolation.RenderTick;
            foreach (var id in _world.GrenadeIds)
            {
                if (!_world.TryShowGrenade(id, renderTick, _grenadeCarrier, out var position, out var rotation))
                {
                    continue;
                }

                if (!_grenadeViews.TryGetValue(id, out var view) || view == null)
                {
                    view = CreateGrenadeView(_world.GrenadeTypeId(id));
                    _grenadeViews[id] = view;
                }

                view.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), NetV2Math.ToUnity(rotation));
            }
        }

        // 自分は予測した位置に、他の人は補間した位置に出しているので、くっついたグレネードもその見せている位置に付ける
        private bool ShownCarrier(int playerId, out System.Numerics.Vector3 position, out float yaw)
        {
            if (playerId == _client.PlayerId)
            {
                position = _match.LocalState.Position;
                yaw = _ownYaw;
                return _local != null;
            }

            if (_match.TrySampleRemote(playerId, out var pose))
            {
                position = pose.Position;
                yaw = pose.Yaw;
                return true;
            }

            position = default;
            yaw = 0f;
            return false;
        }

        private GameObject CreateGrenadeView(string typeId)
        {
            GameObject view;
            if (GrenadePrefab != null)
            {
                view = new GameObject(ThrownGrenade.ObjectName);
                VisualPrefab.Instantiate(GrenadePrefab, view.transform, ThrownGrenade.VisualName, stripColliders: true);
            }
            else
            {
                view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                view.name = ThrownGrenade.ObjectName;
                var spec = KitTuning.Grenades.TryFind(typeId, out var typed) ? typed : ThrownGrenade.UntypedSpec;
                view.transform.localScale = Vector3.one * (spec.BodyRadius * 2f);

                // 当たりはサーバーが取るので、見た目に Unity の物理を持たせない
                Destroy(view.GetComponent<Collider>());
            }

            view.transform.SetParent(_remoteRoot, false);
            return view;
        }

        private void ForgetGrenades(SnapshotMessage snapshot)
        {
            _goneGrenades.Clear();
            _world.ApplyGrenades(snapshot, _goneGrenades);
            foreach (var id in _goneGrenades)
            {
                if (_grenadeViews.TryGetValue(id, out var view))
                {
                    _grenadeViews.Remove(id);
                    if (view != null)
                    {
                        Destroy(view);
                    }
                }
            }
        }

        private void SendRequests()
        {
            if (_local == null)
            {
                return;
            }

            var direction = NetV2Math.ToNumerics(_local.AimDirection);
            Trigger(direction);

            var state = _match.LocalState;
            var feet = NetV2Math.ToUnity(state.Position) + (Vector3.up * _client.Motor.Stance.Shape(state.Stance).BottomY);
            _vehicles.SendRequests(_local, _client, feet);

            if (_local.TakeReload())
            {
                _weaponAction.Action = WeaponAction.Reload;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }

            if (_local.TakeSwitchAmmoType())
            {
                _weaponAction.Action = WeaponAction.SwitchAmmoType;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }

            var pouch = OwnGrenades();
            if (_local.TakeSwitchGrenade() && _grenadeChoice.SelectNext(pouch))
            {
                var typeId = _grenadeChoice.Current(pouch);
                Debug.Log(GrenadeThrower.LogTag + " net selected type=" + typeId + " remaining=" + pouch.RemainingOf(typeId).ToString(CultureInfo.InvariantCulture), this);
                _hud?.ShowGrenades(pouch, typeId);
            }

            if (_local.TakeGrenade())
            {
                _grenade.Direction = direction;
                _grenade.GrenadeTypeId = _grenadeChoice.Current(pouch);
                _client.Send(_grenade, TransportChannel.Reliable);
            }

            // 付けられないと分かっている印は送らない (Netcode の NetTeamDeathmatch.CanRequestPing と同じく、ルールで付けられて生きているときだけ)
            if (_local.TakePing() && CanPing())
            {
                Debug.Log(PingLog.Tag + " net request", this);
                _ping.Direction = direction;
                _client.Send(_ping, TransportChannel.Reliable);
            }
        }

        private GrenadePouchSync OwnGrenades()
        {
            var latest = _client.LatestSnapshot;
            return latest != null && latest.HasOwner ? latest.Owner.Grenades : null;
        }

        private void ShowArea(AreaEffectState state)
        {
            EndArea(state);
            var zone = AreaEffectZone.Show(state.KindId, NetV2Math.ToUnity(state.Center), state.Radius, state.RemainingSeconds);
            _areaZones[state.AreaId] = zone;
        }

        private void UpdateArea(AreaEffectState state)
        {
            if (_areaZones.TryGetValue(state.AreaId, out var zone) && zone != null)
            {
                zone.SetRemaining(state.RemainingSeconds);
                return;
            }

            ShowArea(state);
        }

        private void EndArea(AreaEffectState state)
        {
            if (_areaZones.TryGetValue(state.AreaId, out var zone))
            {
                _areaZones.Remove(state.AreaId);
                if (zone != null)
                {
                    zone.End();
                }
            }
        }

        private bool CanPing()
        {
            var latest = _client.LatestSnapshot;
            if (latest == null || latest.Match.Rules.Ping != Shooter.Core.Ping.On)
            {
                return false;
            }

            var own = latest.Players.IndexOf(_client.PlayerId);
            return own >= 0 && latest.Players[own].Health > 0;
        }

        // 引き金は今の武器の撃ち方 (ITriggerModel) で、押している間の連射の間合いと、押しっぱなしで出る撃ち方の押した・離したを決める
        private void Trigger(System.Numerics.Vector3 direction)
        {
            var held = _local.IsAttackHeld;
            var pressed = _local.TakeAttack();
            var latest = _client.LatestSnapshot;
            var owner = latest != null && latest.HasOwner ? latest.Owner : null;
            var weaponId = ClientFire.CurrentWeaponId(owner);
            var trigger = _weapons.TriggerFor(weaponId);
            if (_heldContinuous != null && _heldContinuous != trigger)
            {
                // 押したまま持ち替えた・武器を失ったときも、押しっぱなしの撃ち方を止めさせる
                _heldContinuous.Drop();
                SendContinuous(direction, _heldContinuousWeaponId, _heldContinuous.Spec, default, TriggerEdge.Released, owner);
            }

            _local.BlocksAds = trigger != null && trigger.BlocksAds;
            if (trigger == null)
            {
                _hud?.ShowCharge(null);
                if (pressed)
                {
                    Debug.LogWarning(LogTag + " cannot fire " + (_weapons.TriggerError ?? "without a weapon"), this);
                }

                return;
            }

            trigger.Tick(Time.deltaTime);
            var pull = trigger.Pull(held, pressed);
            _hud?.ShowCharge(trigger.Model.ChargeFraction);
            if (trigger.IsContinuous)
            {
                PullContinuous(trigger, pull, direction, weaponId, latest, owner);
                return;
            }

            if (pull.Shots <= 0)
            {
                return;
            }

            switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec))
            {
                case FireDecision.Fire:
                    for (var i = 0; i < pull.Shots; i++)
                    {
                        SendFire(direction, weaponId, trigger.Spec, pull, TriggerEdge.None, owner);
                    }

                    _local.ReleaseSprint(SprintInput.ReasonFire);
                    break;
                case FireDecision.Reload:
                    // 押し続けている間は鳴らさず頼み直さない (オフラインの WeaponController と同じ)
                    if (pressed)
                    {
                        _dryFire?.Play(weaponId);
                        RequestReloadIfAuto(latest);
                    }

                    break;
            }
        }

        // 撃てない間に押した分は押したと送らないが、押したと送った後は撃てなくなっても必ず離したと送る (サーバーが照射を止められるように)
        private void PullContinuous(
            ClientTrigger trigger, in TriggerPull pull, System.Numerics.Vector3 direction, string weaponId, SnapshotMessage latest, OwnerSnapshot owner)
        {
            var sending = _heldContinuous == trigger;

            // 離してすぐ押し直したフレームは、前の照射を止めてから次を始める
            if (sending && pull.Released)
            {
                SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Released, owner);
                sending = false;
            }

            if (pull.Pressed && !sending)
            {
                switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec))
                {
                    case FireDecision.Fire:
                        SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Pressed, owner);
                        _heldContinuous = trigger;
                        _heldContinuousWeaponId = weaponId;
                        sending = true;
                        _local.ReleaseSprint(SprintInput.ReasonFire);
                        break;
                    case FireDecision.Reload:
                        _dryFire?.Play(weaponId);
                        RequestReloadIfAuto(latest);
                        break;
                }
            }

            if (sending && !trigger.IsHeld)
            {
                SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Released, owner);
            }
        }

        private void RequestReloadIfAuto(SnapshotMessage latest)
        {
            if (latest != null && latest.Match.Rules.ReloadMode == ReloadMode.Auto)
            {
                _weaponAction.Action = WeaponAction.Reload;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }
        }

        private void SendContinuous(
            System.Numerics.Vector3 direction, string weaponId, WeaponSpec spec, in TriggerPull pull, TriggerEdge edge, OwnerSnapshot owner)
        {
            if (edge == TriggerEdge.Released)
            {
                _heldContinuous = null;
                _heldContinuousWeaponId = null;
            }

            SendFire(direction, weaponId, spec, pull, edge, owner);
        }

        // 撃てるか (弾・間隔) と当たりはサーバーが決める。表示している tick を載せ、サーバーがその時点へ巻き戻して当てる
        private void SendFire(
            System.Numerics.Vector3 direction, string weaponId, WeaponSpec spec, in TriggerPull pull, TriggerEdge edge, OwnerSnapshot owner)
        {
            var renderTick = Math.Max(0f, _match.Interpolation.RenderTick);
            ClientFire.Fill(_fire, owner, weaponId, pull, edge, _lockOn != null ? _lockOn.LockedTargetId : null);
            _fire.Origin = NetV2Math.ToNumerics(_local.EyePosition);
            _fire.Direction = direction;
            _fire.ClientTick = (int)renderTick;
            _fire.InterpolationAlpha = renderTick - (int)renderTick;
            _fire.ShotGeneration = _shotGeneration;
            _fire.ShotIndex = _shotIndex;
            _shotIndex = unchecked(_shotIndex + 1);
            _client.Send(_fire, TransportChannel.Reliable);
            _weaponHooks.LocalFired(_fire, spec);

            // 押しっぱなしの撃ち方の見た目は受け口が出すので、1 発ごとの射撃の演出は出さない
            if (edge == TriggerEdge.None)
            {
                CombatEvents.RaiseShotFired(_local.gameObject, _local.EyePosition, _local.AimDirection, _fire.WeaponId);
            }
        }

        private bool SendReliable(IMessage message)
        {
            if (_client == null || _client.State != MatchClientState.Connected)
            {
                return false;
            }

            _client.Send(message, TransportChannel.Reliable);
            return true;
        }

        private void HandleSnapshot(SnapshotMessage snapshot)
        {
            if (_match == null)
            {
                return;
            }

            _hud?.Apply(snapshot);
            var grenades = snapshot.HasOwner ? snapshot.Owner.Grenades : null;
            _hud?.ShowGrenades(grenades, _grenadeChoice.Current(grenades));
            _weaponHooks.Apply(snapshot);
            ForgetGrenades(snapshot);
            _loadout.Apply(snapshot, _client.PlayerId);
            ShowControlPoints(snapshot);
            PlayReloadSound(snapshot);
        }

        // リロードを始めた (サーバーの自動リロードを含む) ときにリロード音を鳴らす (Netcode の NetPlayer と同じ)
        private void PlayReloadSound(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner)
            {
                return;
            }

            var reloading = snapshot.Owner.Weapon.IsReloading;
            var weaponId = ClientFire.CurrentWeaponId(snapshot.Owner);
            if (reloading && !_wasReloading && weaponId != null && _presentation != null && _presentation.isActiveAndEnabled)
            {
                _presentation.PlayReload(weaponId);
            }

            _wasReloading = reloading;
        }

        private void CollectControlPoints(Scene scene)
        {
            _zones.Clear();
            foreach (var root in scene.GetRootGameObjects())
            {
                _zones.AddRange(root.GetComponentsInChildren<ControlPointZone>(false));
            }
        }

        // 拠点の円盤を持ち主の色で塗る (Netcode の NetControlPoint と同じ)。拠点はマップの番号 (ControlPointZone.Index) で指す
        private void ShowControlPoints(SnapshotMessage snapshot)
        {
            var points = snapshot.ControlPoints;
            foreach (var zone in _zones)
            {
                if (zone == null)
                {
                    continue;
                }

                var at = points.IndexOf(zone.Index);
                Team? owner = at >= 0 && points[at].HasOwner ? points[at].Owner : (Team?)null;
                if (zone.Owner != owner)
                {
                    zone.ShowOwner(owner);
                }
            }
        }

        private void HandleLadderGrabbed(int ladder, float yaw)
        {
            Debug.Log("[Ladder] grab index=" + ladder.ToString(CultureInfo.InvariantCulture)
                + " yaw=" + (yaw * Mathf.Rad2Deg).ToString("F1", CultureInfo.InvariantCulture), this);
            _local?.FaceYaw(yaw);
        }

        private void HandleReconciled(ReconcileResult result)
        {
            if (result.Outcome == ReconcileOutcome.Corrected)
            {
                Debug.Log(LogTag + " reconciled tick=" + result.Tick.ToString(CultureInfo.InvariantCulture)
                    + " error=" + result.Error.ToString("F3", CultureInfo.InvariantCulture)
                    + " resimulated=" + result.ResimulatedTicks.ToString(CultureInfo.InvariantCulture), this);
            }
        }

        private void HandleMessage(IMessage message)
        {
            switch (message)
            {
                case AntiCheatToClientMessage antiCheat:
                    _antiCheat.HandleMessage(antiCheat, Time.realtimeSinceStartupAsDouble);
                    break;
                case KilledMessage killed:
                    var entry = new KillFeedEntry(killed.KillerId, killed.VictimId, killed.WeaponId, killed.Headshot, killed.Friendly);
                    Debug.Log(CombatLogTag + " Killed " + entry, this);
                    if (killed.VictimId == _client.PlayerId && _local != null)
                    {
                        _local.ReleaseHolds(CrouchInput.ReasonDeath);
                    }

                    CombatEvents.RaiseKilled(entry);
                    break;
                case DamageTakenMessage damage:
                    Vector3? source = damage.HasSource ? NetV2Math.ToUnity(damage.SourcePosition) : (Vector3?)null;
                    Debug.Log(CombatLogTag + " DamageTaken amount=" + damage.Amount.ToString(CultureInfo.InvariantCulture)
                        + " source=" + (source.HasValue ? source.Value.ToString("F2", CultureInfo.InvariantCulture) : "none"), this);
                    CombatEvents.RaiseDamageTaken(source, Math.Max(0, damage.Amount));
                    break;
                case HitConfirmMessage hit:
                    var confirm = new HitConfirm(Math.Max(0, hit.Damage), hit.Zone, hit.Killed, hit.Downed);
                    Debug.Log(CombatLogTag + " HitConfirmed damage=" + confirm.Damage.ToString(CultureInfo.InvariantCulture)
                        + " zone=" + confirm.Zone + " killed=" + confirm.Killed + " downed=" + confirm.Downed, this);
                    CombatEvents.RaiseHitConfirmed(confirm);
                    break;
                case ShotMessage shot:
                    ShowShot(shot);
                    break;
                case ExplosionMessage explosion:
                    var center = NetV2Math.ToUnity(explosion.Center);
                    Debug.Log(CombatLogTag + " Explosion weapon=" + explosion.WeaponId + " at=" + center.ToString("F2", CultureInfo.InvariantCulture)
                        + " thrower=" + (explosion.HasThrower ? explosion.ThrowerId.ToString(CultureInfo.InvariantCulture) : "none"), this);
                    ThrownGrenade.PlayExplosionEffects(center);
                    CombatEvents.RaiseExploded(center, explosion.WeaponId);
                    break;
                case TeleportedMessage teleported:
                    HandleTeleported(teleported);
                    break;
                case ShotIndexCorrectedMessage corrected:
                    // Netcode のクライアント (NetShotSequence.Correct) と同じく、まだサーバーが数えていない弾の分も含めて同じだけずらす
                    if (corrected.Generation != _shotGeneration)
                    {
                        _shotIndex = unchecked(_shotIndex + (corrected.ServerIndex - corrected.ClientIndex));
                        _shotGeneration = corrected.Generation;
                    }

                    break;
                case IdleWarningMessage idle:
                    if (idle.Active)
                    {
                        _hud?.ShowIdleWarning(idle.RemainingSeconds);
                    }
                    else
                    {
                        _hud?.ClearIdleWarning();
                    }

                    break;
                case LoadoutAnswerMessage answer:
                    _loadout.Answer(answer);
                    break;
                case InventoryStateMessage inventory:
                    _inventory.Receive(inventory);
                    break;
                case StructureStateMessage structure:
                    _structures.Receive(structure);
                    break;
                case VehicleCorrectedMessage corrected:
                    _match?.Vehicles.Correct(corrected);
                    break;
                default:
                    if (!_weaponHooks.Handle(message))
                    {
                        _pings.Handle(message, Time.time);
                    }

                    break;
            }
        }

        private void DrawBeam(BeamView beam)
        {
            var shooter = ShooterObject(beam.ShooterId);
            var from = NetV2Math.ToUnity(beam.Start);
            var to = NetV2Math.ToUnity(beam.End);

            // 目の位置から線を出すと一人称では線が画面の中心に潰れて見えないので、オフラインの BeamDelivery と同じく銃口から出す
            var presentation = WeaponPresentation.FindFor(shooter);
            if (presentation != null)
            {
                from = presentation.Sockets.MuzzlePose(from, to - from).position;
            }

            BeamLine.Show(shooter, beam.WeaponId, beam.TickSeconds, from, to, Time.time);
        }

        private void HideBeam(int shooterId)
        {
            var shooter = ShooterObject(shooterId);
            if (shooter != null && shooter.TryGetComponent<BeamLine>(out var line))
            {
                line.Hide();
            }
        }

        private void DrawSpray(SprayView spray)
        {
            var shooter = ShooterObject(spray.ShooterId);
            var from = NetV2Math.ToUnity(spray.Start);
            var toward = NetV2Math.ToUnity(spray.End) - from;
            var length = toward.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            var direction = toward / length;

            // 目の位置から出すと一人称では火炎が画面の中心に潰れて見えないので、オフラインの SprayDelivery と同じく銃口から出す
            var presentation = WeaponPresentation.FindFor(shooter);
            if (presentation != null)
            {
                from = presentation.Sockets.MuzzlePose(from, direction).position;
            }

            SprayFlame.Show(shooter, spray.WeaponId, spray.TickSeconds, from, direction, length, spray.ConeDegrees, Time.time);
        }

        private void HideSpray(int shooterId)
        {
            var shooter = ShooterObject(shooterId);
            if (shooter != null && shooter.TryGetComponent<SprayFlame>(out var flame))
            {
                flame.Stop();
            }
        }

        private GameObject ShooterObject(int shooterId)
        {
            if (_client != null && shooterId == _client.PlayerId)
            {
                return _local != null ? _local.gameObject : null;
            }

            return _remotes.TryGetValue(shooterId, out var remote) && remote != null ? remote.gameObject : null;
        }

        private void ShowShot(ShotMessage shot)
        {
            GameObject shooter = null;
            if (shot.HasShooter && _remotes.TryGetValue(shot.ShooterId, out var remote))
            {
                shooter = remote.gameObject;
            }

            // 演出は 1 発ごとに射撃→着弾の順に出す前提 (WeaponPresentation) なので、区間ごとに対で出す
            for (var i = 0; i < shot.SegmentCount; i++)
            {
                ref var segment = ref shot.Segment(i);
                var origin = NetV2Math.ToUnity(segment.Origin);
                var direction = NetV2Math.ToUnity(segment.Direction);

                // 2 区間目以降は、前の着弾から続く弾 (跳弾・貫通) も同じ射撃の別のペレットも、銃声やマズルフラッシュを出し直さない。
                if (i > 0)
                {
                    CombatEvents.RaiseShotContinued(shooter, origin, direction, shot.WeaponId);
                }
                else
                {
                    CombatEvents.RaiseShotFired(shooter, origin, direction, shot.WeaponId);
                }

                if (segment.HasImpact)
                {
                    HitZone? zone = segment.ImpactIsCharacter && segment.HasImpactZone ? segment.ImpactZone : (HitZone?)null;
                    CombatEvents.RaiseImpact(new ImpactInfo(
                        NetV2Math.ToUnity(segment.ImpactPoint), NetV2Math.ToUnity(segment.ImpactNormal), null,
                        segment.ImpactMaterialId ?? "", segment.ImpactIsCharacter, zone));
                }
            }
        }

        private void HandleTeleported(TeleportedMessage teleported)
        {
            if (teleported.PlayerId != _client.PlayerId)
            {
                _match?.ForgetRemoteHistory(teleported.PlayerId);
                return;
            }

            // サーバーは置き直した位置を落ちたときに戻す先にもする (ServerMovement.Teleport)
            if (_match != null)
            {
                _match.Prediction.Simulation.SpawnPosition = teleported.Position;
            }

            _local?.ReleaseHolds(CrouchInput.ReasonRespawn);
        }

        private void AddRemote(int playerId)
        {
            if (_remoteRoot == null || _remotes.ContainsKey(playerId))
            {
                return;
            }

            _remotes.Add(playerId, NetV2RemotePlayer.Create(playerId, _client.Motor.Stance, _remoteRoot));
            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " is in view", this);
        }

        private void RemoveRemote(int playerId)
        {
            _weaponHooks.StatusEffects.Forget(playerId);
            if (!_remotes.TryGetValue(playerId, out var remote))
            {
                return;
            }

            _remotes.Remove(playerId);
            if (remote != null)
            {
                Destroy(remote.gameObject);
            }

            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " left", this);
        }

        // 自分の体は一人称で見えないので、燃えている見た目は相手の体にだけ付け、自分には HUD (NetV2Hud) で出す。
        private void ShowStatusEffect(int playerId, string kindId)
        {
            Debug.Log(LogTag + " status player=" + playerId.ToString(CultureInfo.InvariantCulture) + " kind=" + kindId + " on", this);
            if (_remotes.TryGetValue(playerId, out var remote) && remote != null)
            {
                StatusEffectVisuals.For(remote.gameObject).Show(kindId);
            }
        }

        private void HideStatusEffect(int playerId, string kindId)
        {
            Debug.Log(LogTag + " status player=" + playerId.ToString(CultureInfo.InvariantCulture) + " kind=" + kindId + " off", this);
            if (_remotes.TryGetValue(playerId, out var remote) && remote != null)
            {
                var visuals = remote.GetComponent<StatusEffectVisuals>();
                if (visuals != null)
                {
                    visuals.Hide(kindId);
                }
            }
        }

        // サーバーに切られる前にこちらから抜けても、サーバーが EAC で切ったときと同じ符号で同じ文を出す
        private void HandleAntiCheatRemoved(string reason)
        {
            if (_finished || _client == null)
            {
                return;
            }

            var wasConnected = _client.State == MatchClientState.Connected;
            _client.Leave();
            HandleDisconnected(DisconnectNotice.Classify(
                DisconnectNotice.RejectedReason(AntiCheatSettings.RejectedDetail), wasConnected, _client.ClientVersion));
        }

        private void HandleDisconnected(DisconnectNotice notice)
        {
            _antiCheat?.End();
            if (!notice.WasConnected)
            {
                Leave(ExitConnectFailed, LogTag + " failed to connect to " + _options.Endpoint + " " + notice.Describe(), notice);
                return;
            }

            Debug.Log(LogTag + " disconnected from server " + notice.Describe(), this);
            Finish(MenuReturnReason.Disconnected(notice, ExitDisconnected));
        }

        private void HandleReturningToMenu()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            Debug.Log(LogTag + " leaving for the menu; shutting down the network", this);
            _client?.Leave();
            Destroy(gameObject);
        }

        // 接続を失ったのでなければ、Netcode のクライアントを起動引数で動かしたときと同じく、このシーンに留まってログだけ残す。
        // メニューから始めたときは、Netcode のクライアント (NetBootstrap.ReturnToMenuIfStartedThere) と同じく理由を添えて戻る
        private void Leave(int exitCode, string message, DisconnectNotice notice = null)
        {
            Debug.LogError(message, this);
            _antiCheat?.End();
            _client?.Leave();
            if (notice != null)
            {
                Finish(MenuReturnReason.Disconnected(notice, exitCode));
                return;
            }

            if (_startedFromMenu)
            {
                Finish(MenuReturnReason.OnlineFailed(exitCode, message));
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(exitCode);
            }
        }

        // メニューへ戻る。起動引数でつないだときも、残っても遊べないので戻す (batchmode では先に終了する)
        private void Finish(MenuReturnReason reason)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(reason.ExitCode);
                return;
            }

            var menu = _menu ?? GameUi.Menu;
            if (menu == null)
            {
                Debug.LogError(LogTag + " cannot return to the menu: no menu navigator is registered with GameUi.Menu; staying in this scene", this);
                return;
            }

            // 戻る前に消す。メニューへ戻る知らせ (ReturningToMenu) で自分を消し直さないため
            if (_menu != null)
            {
                _menu.ReturningToMenu -= HandleReturningToMenu;
                _menu = null;
            }

            menu.ReturnToMenu(reason);
            Destroy(gameObject);
        }

        private void DestroyViews()
        {
            _weaponHooks?.StatusEffects.Clear();
            if (_local != null)
            {
                Destroy(_local.gameObject);
            }

            if (_hud != null)
            {
                Destroy(_hud.gameObject);
            }

            if (_remoteRoot != null)
            {
                Destroy(_remoteRoot.gameObject);
            }

            _remotes.Clear();
            _grenadeViews.Clear();
            _world?.ClearGrenades();
            _zones.Clear();
            _weaponHooks?.AreaEffects.Clear();
            _areaZones.Clear();
            _structures?.Leave();
            _vehicles?.Leave();
            _weaponHooks?.Projectiles.Detach();
            _weaponHooks?.Beams.Detach();
            _weaponHooks?.Spray.Detach();
        }

        private static bool TryFindScene(string map, out string path)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var candidate = SceneUtility.GetScenePathByBuildIndex(i);
                var normalized = candidate.Replace('\\', '/');
                if (normalized.EndsWith(_sceneExtension, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileNameWithoutExtension(normalized), map, StringComparison.Ordinal))
                {
                    path = candidate;
                    return true;
                }
            }

            path = null;
            return false;
        }

        /// <summary>
        /// メニューのクイックマッチ。起動引数でサーバーを決めていればそこへ、無ければロビーを探してつなぐ。
        /// ロビーを探せないときは、開発中に <c>make server-dotnet</c> へつなげるよう既定のサーバーへつなぐ。
        /// </summary>
        private sealed class MenuStarter : IOnlineMatchStarter
        {
            private readonly NetLaunchOptions _server;

            public MenuStarter(NetLaunchOptions server)
            {
                _server = server;
            }

            public bool Start(MenuDestination destination, out MenuReturnReason refusal)
            {
                refusal = null;
                if (Current != null)
                {
                    if (!Current._finished)
                    {
                        return true;
                    }

                    // 起動引数で始めて失敗したクライアントは残っているので、消して始め直す (Destroy は遅れるので先に外す)
                    var finished = Current;
                    Current = null;
                    Destroy(finished.gameObject);
                }

                if (_server != null)
                {
                    Begin(_server, true);
                }
                else if (!CanSearchLobbies())
                {
                    var fallback = NetClientLaunch.DefaultServer();
                    Debug.Log(QuickMatchSearch.LogPrefix + "the EOS lobby is not available (no EOS in this build or no credentials); quick match connects to "
                        + fallback.Endpoint);
                    Begin(fallback, true);
                }
                else
                {
                    BeginQuickMatch(true);
                }

                return true;
            }
        }
    }
}
