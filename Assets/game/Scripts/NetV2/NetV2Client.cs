using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    [DisallowMultipleComponent]
    public sealed partial class NetV2Client : MonoBehaviour
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

        // Netcode のクライアントと同じく、同じプロセスのままつなぎ直したときだけ成績を引き継ぐよう、プロセスごとに 1 つ作る。
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
        private readonly List<INetV2ClientPart> _parts = new List<INetV2ClientPart>();
        private readonly ClientMapChange _mapChange = new ClientMapChange();
        private GrenadeCarrier _grenadeCarrier;
        private string _loadingMap;
        private string _joinCode;
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
        private MatchLobby _matchLobby;
        private ClientWorld _world;
        private NetV2LocalPlayer _local;
        private WeaponPresentation _presentation;
        private DryFireSound _dryFire;
        private LockOnAim _lockOn;
        private PingController _pingDisplay;
        private bool _wasReloading;
        private bool _startedFromMenu;
        private NetV2Hud _hud;
        private NetV2NameplateTargets _nameplates;
        private Transform _remoteRoot;
        private IMenuNavigator _menu;
        private AsyncOperation _mapLoad;
        private int _shotIndex;
        private int _shotGeneration;
        private bool _finished;
        private ReplayTransport _replay;
        private ReplayRecorder _recorder;
        private string _recordingPath;

        public MatchClient Client => _client;

        // ファイルから再生している (相手のサーバーがいない) か
        public bool IsReplay => _replay != null;

        public ReplayTransport Replay => _replay;

        public ReplayRecorder Recorder => _recorder;

        public ClientMatch Match => _match;

        public NetV2LocalPlayer LocalPlayer => _local;

        public GameObject GrenadePrefab { get; set; }

        // 部品を作る (つなぐ・再生する) 前に置く。null なら箱と脱出地点の形をコードで作る
        public NetV2LootVisuals LootVisuals { get; set; }

        public static NetV2Client Current { get; private set; }

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
            NetV2OnlineServices.Install();
            NetV2Replays.Install();
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

        public static NetV2Client Begin(NetLaunchOptions options, bool fromMenu)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var client = Create(fromMenu);
            client.Connect(options);
            return client;
        }

        public static NetV2Client BeginQuickMatch(bool fromMenu)
        {
            var client = Create(fromMenu);
            client.SearchServer();
            return client;
        }

        public static string ConnectByJoinCode(JoinCodeResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Outcome != JoinCodeOutcome.Joined)
            {
                return "no server was found for the join code (" + result.Outcome + ")";
            }

            if (Current != null && !Current._finished)
            {
                return "already in an online match";
            }

            ReleaseFinished();
            var client = Create(true);
            client._joinCode = result.JoinCode;
            client._matchLobby = result.MatchLobby;
            client.Connect(result.Options);
            return null;
        }

        // ReplayLibrary の再生の口。読めないファイルや版の違うファイルは、画面を替える前に例外で断り、一覧に理由を出させる (ReplayLibrary.Play)。
        public static bool BeginReplay(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be empty", nameof(path));
            }

            if (Current != null && !Current._finished)
            {
                throw new InvalidOperationException("already in an online match; leave it before playing a replay");
            }

            var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            ReplayTransport replay;
            try
            {
                replay = new ReplayTransport(input, line => Debug.Log(line));
            }
            catch
            {
                input.Dispose();
                throw;
            }

            if (!BuildVersion.TryNormalize(Application.version, out var version, out var versionError))
            {
                replay.Dispose();
                throw new InvalidOperationException("cannot read the build version (Application.version): " + versionError);
            }

            ReleaseFinished();
            var client = Create(true);
            client.Play(replay, version);
            return true;
        }

        private static NetV2Client Create(bool fromMenu)
        {
            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            var client = host.AddComponent<NetV2Client>();
            client._startedFromMenu = fromMenu;
            return client;
        }

        // 起動引数で始めて失敗したクライアントは残っているので、消して始め直す (Destroy は遅れるので先に外す)
        private static void ReleaseFinished()
        {
            if (Current == null || !Current._finished)
            {
                return;
            }

            var finished = Current;
            Current = null;
            Destroy(finished.gameObject);
        }

        // 読めない認証情報のファイルは誤りを出すため、探せない側ではなく探す側に回す。
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

            _search = new QuickMatchSearch(backend, Application.version, line => Debug.Log(line, this))
            {
                JoinsMatchLobby = true,
                PreferredRegion = AppliedControlSettings.Current.QuickMatchRegion,
            };
            var party = NetV2PartyControl.Current;
            _search.Begin(
                Time.unscaledTime,
                party != null ? party.MatchPlayers : 1,
                party != null ? party.SkillAverage : OwnSkillRating(),
                HandleServerFound,
                HandleSearchFailed);
        }

        internal static int? OwnSkillRating()
        {
            var skill = SkillBootstrap.Current;
            return skill != null ? skill.Rating : (int?)null;
        }

        // 通信路がつながった時に読むので、つなぐまでに入ったパーティやログインで決まった ProductUserId も載る。
        private ConnectionExtras BuildExtras()
        {
            var party = NetV2PartyControl.Current;
            return ClientConnectExtras.Compose(
                party != null ? party.PartyId : null, _joinCode, OwnSkillRating(), NetV2Eos.LocalProductUserId(), line => Debug.LogWarning(line, this));
        }

        private void HandleServerFound(NetLaunchOptions options)
        {
            if (_finished)
            {
                return;
            }

            _matchLobby = _search.TakeMatchLobby();
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
            Action<string> log = line => Debug.Log(line, this);
            IMatchTransport transport = eos ? NetV2Eos.Factory.Create(plan.Credentials, log) : new UdpMatchTransport();
            transport = NetV2Replays.Record(transport, version, log, out _recorder, out _recordingPath);
            StartClient(transport, version, log, false);
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

        private void StartClient(IMatchTransport transport, string version, Action<string> log, bool replay)
        {
            _client = new MatchClient(transport, version, _deviceKey, "", BuildExtras, log);
            _client.Accepted += HandleAccepted;
            _client.SnapshotReceived += HandleSnapshot;
            _client.MessageReceived += HandleMessage;
            _client.Disconnected += HandleDisconnected;
            _parts.AddRange(NetV2ClientParts.Create(new NetV2ClientPartsContext(_client, SendReliable, log)
            {
                Pings = _pings.Markers,
                Minimap = MinimapPublisher.Tuning,
                ControlPointPosition = ControlPointPosition,
                MapDecided = _mapChange.Decide,
                LocalPlayer = () => _local,

                // 再生には相手がおらず、ボイスの部屋も無いので、声の部品を作らない
                Voice = replay ? null : NetV2Eos.CreateVoiceSession(_matchLobby?.LobbyId, log),
                VoiceLobbyId = replay ? null : _matchLobby?.LobbyId,
                Abilities = _weapons.Loadout != null ? _weapons.Loadout.Abilities : Array.Empty<AbilityDefinition>(),
                Replay = replay,
                ReplayFreeSpeed = () => KitTuning.Replay.FreeSpeed,
                LootVisuals = LootVisuals,
            }));
        }

        // 再生ではサーバーがいないので、EAC も通信路の選び方も使わず、ファイルを読む通信路を MatchClient に渡して試合中と同じ部品を動かす。
        private void Play(ReplayTransport replay, string version)
        {
            _replay = replay;
            Action<string> log = line => Debug.Log(line, this);
            StartClient(replay, version, log, true);
            NetV2AntiCheat.TryCreate(AntiCheatSettings.Disabled("replay"), _client, HandleAntiCheatRemoved, out _antiCheat, out _);
            _client.Connect(string.Empty, 0);
        }

        // Netcode のクライアントと同じく、EOS を求められても認証情報が揃わなければ UDP でつなぐ。
        // 認証情報のファイルが壊れているときは、黙って UDP にすると誤りに気づけないので止める。
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
            _matchLobby?.Tick();
            if (_client == null || _finished)
            {
                return;
            }

            // 再生の時計は画面の時間で進める。マップを読んでいる間も進めると、読み終えたときに知らせがまとめて届くので、読み終えるまで止める
            if (_replay != null && _mapLoad == null)
            {
                _replay.Advance(Time.unscaledDeltaTime);
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
                EnterMap(_loadingMap);
            }

            if (_match == null)
            {
                return;
            }

            StepOwner(Time.deltaTime);
            _weaponHooks.StatusEffects.Tick(Time.deltaTime);
            foreach (var part in _parts)
            {
                part.Tick(Time.deltaTime);
            }

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

            ReleasePartsAndLobby();
            _match?.Detach();
            _antiCheat?.End();
            _client?.Dispose();
            NetV2Replays.Kept(_recorder, _recordingPath, line => Debug.Log(line));
            _recorder = null;
            _recordingPath = null;
            DestroyViews();
        }

        // ボイスの部品が RTC 部屋を出てからロビーを出るよう、部品を先に閉じる
        private void ReleasePartsAndLobby()
        {
            foreach (var part in _parts)
            {
                part.Dispose();
            }

            _parts.Clear();
            _matchLobby?.Dispose();
            _matchLobby = null;
        }

        private void HandleAccepted()
        {
            if (!_antiCheat.ServerConnected(Time.realtimeSinceStartupAsDouble, out var antiCheatError))
            {
                Leave(ExitTransportFailure, AntiCheatSettings.LogTag + " " + antiCheatError + NetV2AntiCheat.LaunchHint);
                return;
            }

            _loadout.SendSaved();
            LoadMap(_client.Map);
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
                    _client.Motor.Stance, _client.Motor.Motor, _match.Prediction.Simulation.Body.HasHeadroomToStand, NetV2Math.ToUnity(start.Position), OwnYaw());
                _presentation = _local.GetComponent<WeaponPresentation>();
                _dryFire = _local.GetComponent<DryFireSound>();

                // 印を見る画面があるのは自分のプレイヤーだけなので、表示もここにだけ付ける (Netcode の NetPlayer と同じ)
                _pingDisplay = _local.gameObject.AddComponent<PingController>();
                _pingDisplay.ShowShared(_pings.Markers);
                _lockOn = AttachLockOn(_local);
                AttachAimAssist(_local);
                AttachViewmodel(_local);
            }

            for (var i = 0; i < ticks; i++)
            {
                _local.Sample(_match.LocalState.IsGrounded || _match.LocalState.IsInJumpGrace, out var move, out var yaw, out var buttons);
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

        // 回した視点は FirstPersonLook の yaw としてそのまま入力で送られるので、サーバーに知らせることは無い。
        private void AttachAimAssist(NetV2LocalPlayer local)
        {
            var assist = local.gameObject.AddComponent<AimAssist>();
            assist.Look = local.Look;
            assist.AimingSource = () => local != null && local.IsAiming;
            assist.AimSource = () => local != null ? new Ray(local.EyePosition, local.AimDirection) : (Ray?)null;
            assist.Targets = new ClientHostileAimTargets(
                _client.PlayerId,
                () => _client.LatestSnapshot,
                _match.TrySampleRemote,
                _world.Collision,
                _client.Motor.Stance,
                (eye, point) => EnemySenses.IsVisionBlocked(eye.ToUnity(), point.ToUnity()));
        }

        // 持っている武器と付けた物はサーバーのスナップショットと在庫で決まるので、オフラインの WeaponController の代わりにそれを渡す。迷彩は ViewmodelPresenter が着けている見た目から引く。
        private void AttachViewmodel(NetV2LocalPlayer local)
        {
            var presenter = local.AttachViewmodel();
            presenter.WeaponIdSource = () => ClientFire.CurrentWeaponId(CurrentOwner());
            presenter.AttachmentsSource = weaponId => _inventory.Inventory?.AttachmentsOf(weaponId);

            // 地図を替えるときは _match を外したフレームの終わりまで体が残る (Destroy) ので、その間は試合が無いものとして隠す。
            presenter.HiddenReasonSource = () => _match == null || _client == null
                ? ViewmodelPresenter.ReasonDisabled
                : NetV2Viewmodel.HiddenReason(_client.LatestSnapshot, _client.PlayerId, _match.LocalState, _vehicles.HidesOwnWeapon());
            presenter.Motion.HorizontalSpeedSource = () => _match != null ? NetV2Viewmodel.HorizontalSpeed(_match.LocalState) : 0f;
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

            // 再生の終わりは切断として届くが、切れたのではないので、メニューに理由を出さずに戻る
            if (_replay != null)
            {
                Finish(null);
                return;
            }

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

            // 起動引数で始めたクライアントは失敗しても消えずに残るので、ロビーの席を空けておく
            ReleasePartsAndLobby();
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
                Application.Quit(reason != null ? reason.ExitCode : ExitDisconnected);
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

            // 他の画面が後から出した引き先まで消さないよう、自分が出した値のときだけ消す。
            if (_nameplates != null)
            {
                GameSignals.NameplateTargets.ClearIfCurrent(_nameplates);
                _nameplates = null;
            }
        }

        // ロビーを探せないときに既定のサーバーへつなぐのは、開発中に make server-dotnet へつなげるようにするため。
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
                if (Current != null && !Current._finished)
                {
                    return true;
                }

                ReleaseFinished();
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

    public interface INetV2ClientPart : IDisposable
    {
        bool Handle(IMessage message);

        void Apply(SnapshotMessage snapshot);

        void Tick(float deltaSeconds);
    }
}
