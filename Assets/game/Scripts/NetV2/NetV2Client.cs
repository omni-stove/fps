using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Protocol;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.NetV2
{
    /// <summary>
    /// ゲームのネット対戦のクライアント。Fps.Protocol と LiteNetLib (<see cref="UdpMatchTransport"/>) か、
    /// <c>-transport eos</c> なら EOS の P2P (<see cref="NetV2Eos"/>) で .NET のサーバーへつなぎ、
    /// 承認されたらサーバーのマップのシーンへ移って、自分のプレイヤー (<see cref="NetV2LocalPlayer"/>)・他のプレイヤー
    /// (<see cref="NetV2RemotePlayer"/>)・HUD (<see cref="NetV2Hud"/>) を作る。シーンをまたいで残る。
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
    /// メニューのクイックマッチ (<see cref="MenuNavigator.OnlineStarter"/>) は、<c>-address</c>・<c>-port</c>・<c>-eos-server</c> があればそのサーバーへ、
    /// 無ければロビーを探してつなぐ。EOS のロビーが使えない (EOS がビルドに無い・認証情報が揃わない) UDP のときは、
    /// <see cref="NetClientLaunch.DefaultServer"/> へつなぐ。
    /// </para>
    /// <para>
    /// サーバーとの接続を失ったら (つながらない・断られたを含む)、Netcode のクライアントと同じ理由の文 (<see cref="DisconnectNotice"/>) を添えてメニューへ戻る。
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
        private readonly ClientOverheat _overheat = new ClientOverheat();
        private NetV2Structures _structures;
        private ClientWeaponHooks _weaponHooks;
        private ClientTrigger _heldContinuous;
        private string _heldContinuousWeaponId;
        private NetV2LoadoutChooser _loadout;
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

            MenuNavigator.OnlineStarter = new MenuStarter(server);
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
                    + "the EOS lobby is not in this build (is com.playeveryware.eos installed and Fps.NetEos compiled?)");
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
            _weaponHooks = new ClientWeaponHooks(_weapons.SpecFor);
            _weaponHooks.StatusEffects.Started += ShowStatusEffect;
            _weaponHooks.StatusEffects.Ended += HideStatusEffect;
            _weaponHooks.AreaEffects.Started += ShowArea;
            _weaponHooks.AreaEffects.Updated += UpdateArea;
            _weaponHooks.AreaEffects.Ended += EndArea;
            _pings = new ClientPings(line => Debug.Log(line, this));
            _loadout = new NetV2LoadoutChooser(_weapons, SendReliable, this);
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
                    error = LogTag + " the EOS transport is not in this build (is com.playeveryware.eos installed and Fps.NetEos compiled?)";
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
            SendRequests();
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
            _match = new ClientMatch(_client, _world.CreateSimulation(motor, _client.TickRate, _client.KillPlaneY));
            _match.PlayerAdded += AddRemote;
            _match.PlayerRemoved += RemoveRemote;
            _match.Reconciled += HandleReconciled;
            _match.LadderGrabbed += HandleLadderGrabbed;
            var scene = SceneManager.GetActiveScene();
            _structures.Enter(scene, _world);
            CollectControlPoints(scene);
            _remoteRoot = new GameObject("NetV2Players").transform;
            foreach (var id in _match.RemotePlayerIds)
            {
                AddRemote(id);
            }

            try
            {
                _hud = NetV2Hud.Create(_client.PlayerId, _client.MaxHealth, _weapons.SpecFor);
                _hud.BindStatusEffects(_weaponHooks.StatusEffects);
                _loadout.Bind(_hud.Hud.LoadoutSelect);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(LogTag + " cannot create the HUD: " + e.Message, this);
            }

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
            }

            for (var i = 0; i < ticks; i++)
            {
                _local.Sample(_match.LocalState.IsGrounded, out var move, out var yaw, out var buttons);
                _match.Step(NetV2Math.ToNumerics(move), yaw, buttons);
            }

            _match.SendInput();
            _local.Show(_match.LocalState);
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

        private void SendRequests()
        {
            if (_local == null)
            {
                return;
            }

            var direction = NetV2Math.ToNumerics(_local.AimDirection);
            Trigger(direction);

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
            if (latest == null || latest.Match.Rules.Ping != Fps.Core.Ping.On)
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
                if (pressed)
                {
                    Debug.LogWarning(LogTag + " cannot fire " + (_weapons.TriggerError ?? "without a weapon"), this);
                }

                return;
            }

            trigger.Tick(Time.deltaTime);
            var pull = trigger.Pull(held, pressed);
            if (trigger.IsContinuous)
            {
                PullContinuous(trigger, pull, direction, weaponId, latest, owner);
                return;
            }

            if (pull.Shots <= 0)
            {
                return;
            }

            switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec, _overheat))
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
                        _weaponAction.Action = WeaponAction.Reload;
                        _client.Send(_weaponAction, TransportChannel.Reliable);
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
                switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec, _overheat))
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
                        _weaponAction.Action = WeaponAction.Reload;
                        _client.Send(_weaponAction, TransportChannel.Reliable);
                        break;
                }
            }

            if (sending && !trigger.IsHeld)
            {
                SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Released, owner);
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
            ClientFire.Fill(_fire, owner, weaponId, pull, edge, null);
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

            _overheat.Observe(snapshot);
            _hud?.Apply(snapshot);
            var grenades = snapshot.HasOwner ? snapshot.Owner.Grenades : null;
            _hud?.ShowGrenades(grenades, _grenadeChoice.Current(grenades));
            _weaponHooks.Apply(snapshot);
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
                case StructureStateMessage structure:
                    _structures.Receive(structure);
                    break;
                default:
                    if (!_weaponHooks.Handle(message))
                    {
                        _pings.Handle(message, Time.time);
                    }

                    break;
            }
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

                // 前の区間の着弾から続く区間 (跳弾・貫通) は弾の続きなので、銃声やマズルフラッシュを出し直さない。
                if (i > 0 && ContinuesFrom(ref shot.Segment(i - 1), segment.Origin))
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

        private static bool ContinuesFrom(ref ShotSegment previous, System.Numerics.Vector3 origin)
        {
            return previous.HasImpact && System.Numerics.Vector3.DistanceSquared(previous.ImpactPoint, origin) < 1e-6f;
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
            Finish(ExitDisconnected, notice.Message);
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
                Finish(exitCode, notice.Message);
                return;
            }

            if (_startedFromMenu)
            {
                Finish(exitCode, LocalizedText.Literal(message));
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(exitCode);
            }
        }

        // メニューへ戻る。起動引数でつないだときも、残っても遊べないので戻す (batchmode では先に終了する)
        private void Finish(int exitCode, LocalizedText notice)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(exitCode);
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

            menu.ReturnToMenu(notice);
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
            _zones.Clear();
            _weaponHooks?.AreaEffects.Clear();
            _areaZones.Clear();
            _structures?.Leave();
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

            public bool Start(MenuDestination destination, out LocalizedText refusal)
            {
                refusal = default;
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
