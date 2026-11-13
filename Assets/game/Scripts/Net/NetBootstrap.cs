using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// コマンドライン引数 (<c>-mode host|server|client</c> / <c>-address</c> / <c>-port</c> /
    /// <c>-transport udp|eos</c> / <c>-eos-server</c> / <c>-noeac</c> / <c>-eac</c>) に従って NetworkManager を組み立てて起動する。
    /// Easy Anti-Cheat を使うかは <see cref="AntiCheatSettings"/> が決め、<c>[AntiCheat]</c> 付きでログに出す。
    /// 引数が無ければ <see cref="DefaultMode"/> (既定は Host) の UDP で起動する。
    /// 専用サーバービルド (<c>UNITY_SERVER</c>) では引数が無くても Server で起動し、描画・音声のコンポーネントを止める。
    /// EOS を要求されても認証情報が無ければ UDP に切り替え、<see cref="CredentialsMissingLog"/> を出す。
    /// <c>-lobby create|join</c> なら EOS にログインしてロビーを作る (または探して入る) までを <see cref="LobbyLauncher"/> に任せ、
    /// オーナーは EOS の Host、メンバーはオーナーへ接続するクライアントとして起動する。
    /// <c>-lobby quickmatch</c> なら EOS にログインして専用サーバーのロビーを探し、選んだサーバーの公開アドレスへ UDP のクライアントとして接続する。
    /// UDP のサーバーに <c>-register-lobby &lt;公開アドレス:ポート&gt;</c> が付いていれば、待ち受けを始めたところで
    /// <see cref="ServerLobbyRegistrar"/> に EOS のロビーへ登録させ、接続人数を知らせ続け、終了時にロビーを閉じる。
    /// 失敗はすべて <c>[Net]</c> か <c>[Lobby]</c> 付きのエラーログに出し、batchmode のプレイヤーでは終了コード付きで終了する。
    /// メインメニューの「ロビーを作る／入る／クイックマッチ」から来たときは、<see cref="MenuNavigator.TakeLobbyRequest"/> の設定を <c>-lobby</c> と同じに扱い
    /// (起動引数が優先、<see cref="NetStartup.ChooseLobby"/>)、失敗ではその理由を添えてメニューへ戻る。
    /// クライアントがサーバーとの接続を失ったとき (つながらない・承認で断られたのを含む) は、どこから来たかによらず
    /// <see cref="DisconnectNotice"/> で分けた理由 (サーバー停止・タイムアウト・拒否・版違い) を添えてメニューへ戻る。
    /// サーバー (Host を含む) は止まる前に、つないでいるクライアントへ止まると知らせてから切る。
    /// どこからメニューへ戻るときも (<see cref="MenuNavigator.ReturningToMenu"/>)、接続を切ってこの GameObject を消す。
    /// <para>
    /// <c>-bots</c> は試合の進行役を Spawn する前に <see cref="NetTeamDeathmatch.LaunchBots"/> へ渡す (Bot を足すのは試合の .Bots.cs)。
    /// <c>-map</c> は .Map.cs、<c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c> は .NetSimulation.cs、
    /// <c>-drain-timeout</c> と終了の要求は .Shutdown.cs、試合のイベントのログは .Log.cs が受け持つ。
    /// それぞれの起動の前 (<c>*BeforeStart</c>)・起動の後 (<c>*AfterStart</c>)・終了の要求 (<c>*QuitRequested</c>) の差し込み口を
    /// <see cref="StartNetwork"/> と <see cref="HandleWantsToQuit"/> から呼ぶ。
    /// <c>-map</c> のシーンが今のシーンと違えば、NetworkManager を組み立てる前に .Map.cs (<c>MapHandOff</c>) がそのシーンへ移り、
    /// 起動はそのシーンの NetBootstrap に任せる。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NetBootstrap : MonoBehaviour
    {
        /// <summary>このコンポーネントを置くシーン。<c>Fps.Editor.NetSceneBuilder</c> が生成する。</summary>
        public const string ScenePath = "Assets/game/Scenes/NetTest.unity";

        public const int ExitDisconnected = 0;
        public const int ExitInvalidArguments = 2;
        public const int ExitMisconfigured = 3;
        public const int ExitStartFailed = 4;
        public const int ExitConnectFailed = 5;
        public const int ExitTransportFailure = 6;
        public const int ExitLobbyFailed = 7;

        /// <summary>EOS を要求されたが認証情報が揃わず、UDP に切り替えたときのログ。</summary>
        public const string CredentialsMissingLog = "[Net] EOS credentials missing, using UDP";

        /// <summary>終了の前に、登録したロビーを閉じ終えるのを待つ最長の秒数。</summary>
        public const float ServerLobbyCloseWaitSeconds = 3f;

        /// <summary><see cref="QuitIfHeadlessPlayer"/> が頼んだ終了コード。終了をロビーの片付けで待たせた後の終了に使う。</summary>
        private static int _requestedExitCode;

        [SerializeField]
        [Tooltip("接続したクライアントごとに生成するプレハブ (NetworkObject 必須)")]
        private GameObject _playerPrefab;

        [SerializeField]
        [Tooltip("サーバー起動時に Spawn する試合の進行役のプレハブ (NetworkObject と NetTeamDeathmatch 必須)")]
        private GameObject _matchPrefab;

        [SerializeField]
        [Tooltip("-mode が無いときの起動モード")]
        private NetLaunchMode _defaultMode = NetLaunchMode.Host;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("サーバーが接続人数をログに出す間隔 (秒)")]
        private float _statusLogInterval = 1f;

        private NetworkManager _manager;
        private NetLaunchOptions _options;
        private EosCredentials _lobbyCredentials;
        private LobbyService _lobby;
        private LobbyLauncher _lobbyLauncher;
        private ServerLobbyRegistrar _serverLobby;
        private readonly HashSet<ulong> _remoteClients = new HashSet<ulong>();
        private bool _serverLobbyClosing;
        private bool _serverLobbyClosed;
        private float _serverLobbyCloseDeadline;
        private bool _quitHeldForServerLobby;
        private bool _connectedToServer;
        private bool _toldClientsServerStopping;
        private bool _failed;
        private bool _startedFromMenu;
        private bool _returningToMenu;
        private float _nextStatusLogTime;

        public GameObject PlayerPrefab
        {
            get => _playerPrefab;
            set => _playerPrefab = value;
        }

        public GameObject MatchPrefab
        {
            get => _matchPrefab;
            set => _matchPrefab = value;
        }

        public NetLaunchMode DefaultMode
        {
            get => _defaultMode;
            set => _defaultMode = value;
        }

        /// <summary>
        /// プレイヤーを <c>-mode</c> 付きで起動したときと専用サーバーでは、最初のシーンが NetTest でなければ NetTest へ切り替える。
        /// ふだんの起動 (引数なし) では何もしない。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadNetSceneWhenRequested()
        {
            if (!NetStartup.ShouldLoadNetScene(
                    Application.isEditor, NetStartup.IsDedicatedServerBuild, Environment.GetCommandLineArgs()))
            {
                return;
            }

            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                return;
            }

            var buildIndex = SceneUtility.GetBuildIndexByScenePath(ScenePath);
            if (buildIndex < 0)
            {
                Debug.LogError("[Net] " + ScenePath + " is not in the build settings; run Fps.Editor.NetSceneBuilder.Build and rebuild");
                QuitIfHeadlessPlayer(ExitMisconfigured);
                return;
            }

            SceneManager.LoadScene(buildIndex);
        }

        private void Awake()
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsListening)
                {
                    // Netcode がサーバーのシーンに合わせて読んだシーンにも NetBootstrap があるので、動いている方を使い続ける
                    Debug.Log("[Net] scene " + gameObject.scene.name + " was loaded while the network is running; removing its " + name);
                }
                else
                {
                    Debug.LogWarning("[Net] a NetworkManager already exists; destroying duplicate " + name);
                }

                Destroy(gameObject);
                return;
            }

            var isDedicatedServer = NetStartup.IsDedicatedServerBuild;

            // 起動引数が不正で先に止まっても、メニューで選んだ要求を次の起動まで持ち越さないよう最初に受け取る
            var menuLobby = MenuNavigator.TakeLobbyRequest();
            MenuNavigator.ReturningToMenu += HandleReturningToMenu;

            // サーバーとクライアントを同じマシンで並べて動かすので、裏に回っても止めない
            Application.runInBackground = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, isDedicatedServer))
            {
                // 描画しないと CPU を使い切るまで回るので、ティック (既定 30Hz) より十分速い程度に抑える
                Application.targetFrameRate = 60;
            }

            if (NetStartup.ShouldDisablePresentation(isDedicatedServer))
            {
                var disabled = HeadlessPresentation.DisableIn(gameObject.scene);
                Debug.Log("[Net] dedicated server build: disabled " + disabled + " camera/audio component(s)");
            }

            var args = Environment.GetCommandLineArgs();
            if (!NetStartup.TryResolveLobby(args, isDedicatedServer, out var lobbyOptions, out var lobbyError))
            {
                Fail(ExitInvalidArguments, "[Lobby] invalid arguments: " + lobbyError);
                return;
            }

            lobbyOptions = NetStartup.ChooseLobby(lobbyOptions, args, isDedicatedServer, menuLobby);
            _startedFromMenu = menuLobby.IsRequested && ReferenceEquals(lobbyOptions, menuLobby);
            if (_startedFromMenu)
            {
                Debug.Log("[Lobby] " + LobbyLaunchOptions.CommandName(lobbyOptions.Command) + " requested from the menu (" + lobbyOptions + ")");
            }

            if (!lobbyOptions.IsRequested
                && !NetStartup.TryResolve(args, isDedicatedServer, _defaultMode, out _options, out var error))
            {
                Fail(ExitInvalidArguments, "[Net] invalid arguments: " + error);
                return;
            }

            if (_playerPrefab == null)
            {
                Fail(ExitMisconfigured, "[Net] player prefab is not assigned on " + name + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            if (_matchPrefab == null || _matchPrefab.GetComponent<NetTeamDeathmatch>() == null)
            {
                Fail(ExitMisconfigured, "[Net] match prefab with NetTeamDeathmatch is not assigned on " + name + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            if (lobbyOptions.IsRequested)
            {
                // 起動モードと接続先はロビーが決まってから分かるので、NetworkManager はそのときに組み立てる
                PrepareLobby(lobbyOptions);
                return;
            }

            // EOS のプラットフォームは 1 プロセスで 1 度しか作れないので、transport やロビーを組み立てる前に移る
            var handedOff = false;
            MapHandOff(ref handedOff);
            if (handedOff)
            {
                return;
            }

            var plan = PlanTransport(_options);
            if (plan == null)
            {
                return;
            }

            if (ConfigureNetwork(plan) && _options.RegistersLobby)
            {
                PrepareServerLobby();
            }
        }

        /// <summary>ロビーのサービスを用意する。EOS の認証情報が無ければ UDP には切り替えず失敗にする。</summary>
        private void PrepareLobby(LobbyLaunchOptions lobbyOptions)
        {
            if (!TryCreateLobbyService(
                    LobbyLaunchOptions.LobbyFlag, lobbyOptions.SearchBucketId(Application.version), null, out _lobbyCredentials))
            {
                return;
            }

            _lobby.Removed += HandleLobbyRemoved;
            _lobbyLauncher = new LobbyLauncher(_lobby, lobbyOptions, Application.version);
        }

        /// <summary>
        /// <c>-register-lobby</c> のために、サーバー用の表示名でロビーのサービスと <see cref="ServerLobbyRegistrar"/> を用意する。
        /// 登録は待ち受けを始めてから行う (<see cref="HandleServerStarted"/>)。
        /// </summary>
        private void PrepareServerLobby()
        {
            if (!TryCreateLobbyService(
                    NetLaunchOptions.RegisterLobbyFlag, ServerLobby.BucketId(Application.version), ServerLobby.DisplayName, out _))
            {
                return;
            }

            _serverLobby = new ServerLobbyRegistrar(_lobby, _options.RegisterLobbyEndpoint, Application.version);
            if (!Application.isEditor)
            {
                // エディタでは Play Mode を止めたときの OnDestroy で閉じるので、終了を待たせるのはプレイヤーだけにする
                Application.wantsToQuit += HoldQuitUntilServerLobbyClosed;
            }
        }

        /// <summary>
        /// EOS の認証情報を読み、ロビーのバックエンドから <see cref="_lobby"/> を作る。
        /// 認証情報が無ければ UDP には切り替えず、<paramref name="flag"/> の名前で失敗にして false を返す。
        /// </summary>
        private bool TryCreateLobbyService(string flag, string bucketId, string displayName, out EosCredentials credentials)
        {
            if (!EosCredentialSource.TryLoad(out credentials, out var missing, out var loadError))
            {
                Fail(ExitMisconfigured, "[Lobby] cannot load EOS credentials: " + loadError);
                return false;
            }

            if (credentials == null)
            {
                Fail(ExitMisconfigured, "[Lobby] " + flag + " needs EOS credentials; missing "
                    + string.Join(", ", missing) + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")");
                return false;
            }

            var factory = NetTransportFactories.EosLobby;
            if (factory == null)
            {
                Fail(ExitMisconfigured,
                    "[Lobby] the EOS lobby is not in this build (is com.playeveryware.eos installed and Fps.NetEos compiled?)");
                return false;
            }

            var backend = factory(credentials, displayName);
            if (backend == null)
            {
                Fail(ExitMisconfigured, "[Lobby] the EOS lobby factory returned no backend");
                return false;
            }

            _lobby = new LobbyService(backend, bucketId);

            // EOS のプラットフォームも終了時に解放されるので、それより先に (ここで先に登録して) ロビーから出る要求を出す
            Application.quitting += LeaveLobbyOnQuit;
            return true;
        }

        /// <summary>
        /// サーバーが実行中に Spawn するプレハブ。試合の進行役 matchPrefab と、その <see cref="NetTeamDeathmatch.GrenadePrefab"/>。
        /// グレネードのプレハブが使えなければ (<see cref="NetTeamDeathmatch.TryValidateGrenadePrefab"/>) 登録せずに警告を出す
        /// (サーバーもクライアントも同じ NetMatch プレハブから決めるので、どちらも登録しない側にそろう)。
        /// </summary>
        internal static GameObject[] SpawnablePrefabs(GameObject matchPrefab)
        {
            if (matchPrefab == null)
            {
                throw new ArgumentNullException(nameof(matchPrefab));
            }

            var match = matchPrefab.GetComponent<NetTeamDeathmatch>();
            var grenade = match != null ? match.GrenadePrefab : null;
            if (!NetTeamDeathmatch.TryValidateGrenadePrefab(grenade, out var error))
            {
                Debug.LogWarning("[Net] " + error + " on " + matchPrefab.name + "; grenades cannot be thrown online"
                    + "; run Fps.Editor.NetSceneBuilder.Build");
                return new[] { matchPrefab };
            }

            return new[] { matchPrefab, grenade };
        }

        /// <summary>NetworkManager と transport を組み立て、イベントを購読する。失敗したら false。</summary>
        private bool ConfigureNetwork(NetTransportPlan plan)
        {
            _options = plan.Options;

            try
            {
                _manager = NetworkSetup.Configure(gameObject, plan, _playerPrefab, SpawnablePrefabs(_matchPrefab));
            }
            catch (ArgumentException e)
            {
                Fail(ExitMisconfigured, "[Net] cannot configure NetworkManager: " + e.Message);
                return false;
            }
            catch (InvalidOperationException e)
            {
                Fail(ExitMisconfigured, "[Net] cannot configure NetworkManager: " + e.Message);
                return false;
            }

            _manager.OnServerStarted += HandleServerStarted;
            _manager.OnClientConnectedCallback += HandleClientConnected;
            _manager.OnClientDisconnectCallback += HandleClientDisconnected;
            _manager.OnTransportFailure += HandleTransportFailure;
            // 専用サーバーは docker stop (SIGTERM) で終了するので、Netcode が終了で止まる前にクライアントへ知らせる
            Application.quitting += TellClientsServerStopping;
            Application.wantsToQuit += HandleWantsToQuit;
            return true;
        }

        /// <summary>
        /// EOS を要求されていれば認証情報を読み、使う transport を <see cref="NetTransportPlan"/> に決めさせる。
        /// あわせて Easy Anti-Cheat の設定を決めて計画に載せる。
        /// 認証情報のファイルが壊れている、または EAC の引数が矛盾していれば失敗として null を返す。
        /// </summary>
        private NetTransportPlan PlanTransport(NetLaunchOptions requested)
        {
            EosCredentials credentials = null;
            IReadOnlyList<string> missing = null;
            if (requested.Transport == NetTransportKind.Eos
                && !EosCredentialSource.TryLoad(out credentials, out missing, out var loadError))
            {
                Fail(ExitMisconfigured, "[Net] cannot load EOS credentials: " + loadError);
                return null;
            }

            var plan = NetTransportPlan.Decide(requested, credentials, missing);
            if (plan.FellBackToUdp)
            {
                Debug.LogWarning(CredentialsMissingLog);
                Debug.Log("[Net] missing EOS settings: " + string.Join(", ", plan.MissingCredentials)
                    + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")");
            }

            return WithAntiCheat(plan);
        }

        /// <summary>
        /// Easy Anti-Cheat の設定を決めて計画に載せる。EAC の引数が矛盾していれば失敗として null を返す。
        /// UDP に切り替えた後の設定で決める。EAC は EOS の ProductUserId でピアを登録するので UDP では使えない。
        /// </summary>
        private NetTransportPlan WithAntiCheat(NetTransportPlan plan)
        {
            if (!AntiCheatSettings.TryResolve(
                    Environment.GetCommandLineArgs(), Application.isEditor, plan.Options, out var antiCheat, out var antiCheatError))
            {
                Fail(ExitInvalidArguments, AntiCheatSettings.LogTag + " invalid arguments: " + antiCheatError);
                return null;
            }

            Debug.Log(antiCheat.Describe());
            return plan.WithAntiCheat(antiCheat);
        }

        private void Start()
        {
            if (_failed)
            {
                return;
            }

            if (_lobbyLauncher != null)
            {
                _lobbyLauncher.Begin(Time.unscaledTime, HandleLobbyReady, HandleLobbyFailed);
                return;
            }

            if (_manager == null)
            {
                return;
            }

            StartNetwork();
        }

        /// <summary>ロビーが決まった。その設定で NetworkManager を組み立てて起動する。</summary>
        private void HandleLobbyReady(NetLaunchOptions options)
        {
            if (_failed)
            {
                return;
            }

            var plan = WithAntiCheat(NetTransportPlan.Decide(options, _lobbyCredentials, null));
            if (plan != null && ConfigureNetwork(plan))
            {
                StartNetwork();
            }
        }

        private void HandleLobbyFailed(string reason)
        {
            Fail(ExitLobbyFailed, "[Lobby] " + reason);
        }

        private void HandleLobbyRemoved(string reason)
        {
            // 試合の接続は transport が見ているので、ここでは止めない。Host が抜ければクライアント側は切断で終わる
            Debug.LogWarning("[Lobby] no longer in the lobby (" + reason + "); the match keeps running until the connection ends");
        }

        private void LeaveLobbyOnQuit()
        {
            CloseServerLobby();
            _lobby?.LeaveLobby();
        }

        private void HandleServerLobbyFailed(string reason)
        {
            Fail(ExitLobbyFailed, "[Lobby] " + reason);
        }

        /// <summary>登録したロビーを閉じ始める。閉じ終えるか <see cref="ServerLobbyCloseWaitSeconds"/> が過ぎるまで終了を待たせる。</summary>
        private void CloseServerLobby()
        {
            if (_serverLobby == null || _serverLobbyClosing)
            {
                return;
            }

            _serverLobbyClosing = true;
            _serverLobbyCloseDeadline = Time.unscaledTime + ServerLobbyCloseWaitSeconds;
            _serverLobby.Close(() => _serverLobbyClosed = true);
        }

        /// <summary>
        /// 終了の要求を、登録したロビーを閉じ終えるまで待たせる。閉じる要求を出してすぐ EOS のプラットフォームを解放すると
        /// 要求が届かず、EOS が接続切れに気づくまで、つながらないサーバーのロビーが検索に残る。
        /// </summary>
        private bool HoldQuitUntilServerLobbyClosed()
        {
            CloseServerLobby();
            if (_serverLobby == null || _serverLobbyClosed || Time.unscaledTime >= _serverLobbyCloseDeadline)
            {
                return true;
            }

            if (!_quitHeldForServerLobby)
            {
                Debug.Log("[Lobby] closing the server lobby before quitting (up to " + ServerLobbyCloseWaitSeconds + "s)");
            }

            _quitHeldForServerLobby = true;
            return false;
        }

        /// <summary>待たせていた終了を、ロビーを閉じ終えたか待つ期限が過ぎたところで行う。</summary>
        private void QuitWhenServerLobbyClosed()
        {
            if (!_quitHeldForServerLobby || (!_serverLobbyClosed && Time.unscaledTime < _serverLobbyCloseDeadline))
            {
                return;
            }

            _quitHeldForServerLobby = false;
            if (!_serverLobbyClosed)
            {
                Debug.LogWarning("[Lobby] the server lobby did not close within " + ServerLobbyCloseWaitSeconds + "s; quitting anyway");
            }

            Application.Quit(_requestedExitCode);
        }

        private void StartNetwork()
        {
            // StartHost は接続のコールバックを同期で呼ぶので、開始のログは先に出す
            Debug.Log("[Net] starting " + _options + " transport=" + NetLaunchOptions.TransportName(_options.Transport));

            // transport の設定は待ち受け・接続を始めた後に変えても効かないので、起動の前に差し込み口を呼ぶ
            string beforeStartError = null;
            MapBeforeStart(ref beforeStartError);
            NetSimulationBeforeStart(ref beforeStartError);
            ShutdownBeforeStart(ref beforeStartError);
            LogBeforeStart(ref beforeStartError);
            if (beforeStartError != null)
            {
                Fail(ExitMisconfigured, beforeStartError);
                return;
            }

            var started = false;
            try
            {
                switch (_options.Mode)
                {
                    case NetLaunchMode.Host:
                        started = _manager.StartHost();
                        break;
                    case NetLaunchMode.Server:
                        started = _manager.StartServer();
                        break;
                    case NetLaunchMode.Client:
                        started = _manager.StartClient();
                        break;
                }
            }
            catch (Exception e)
            {
                // 起動処理の外側の境界なので、何が飛んできてもログと終了コードに変える
                Debug.LogException(e);
                started = false;
            }

            if (!started)
            {
                var hint = _options.Mode == NetLaunchMode.Client || _options.Transport != NetTransportKind.Udp
                    ? ""
                    : " (is the port already in use?)";
                Fail(ExitStartFailed, "[Net] failed to start " + _options + hint);
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + _statusLogInterval;
            MapAfterStart();
            NetSimulationAfterStart();
            ShutdownAfterStart();
            LogAfterStart();
        }

        /// <summary>
        /// 終了の要求 (<see cref="Application.wantsToQuit"/>) を差し込み口に渡す。どれかが allowQuit を false にすれば終了を待たせる。
        /// 待たせた差し込み口は、待ち終えたら自分で <see cref="Application.Quit()"/> を呼ぶ。
        /// </summary>
        private bool HandleWantsToQuit()
        {
            var allowQuit = true;
            MapQuitRequested(ref allowQuit);
            NetSimulationQuitRequested(ref allowQuit);
            ShutdownQuitRequested(ref allowQuit);
            LogQuitRequested(ref allowQuit);
            return allowQuit;
        }

        private void Update()
        {
            // ロビーが決まった後もメンバーの出入りを受け取るため、終わるまで進め続ける
            _lobbyLauncher?.Tick(Time.unscaledTime);
            _serverLobby?.Tick(Time.unscaledTime);
            QuitWhenServerLobbyClosed();

            if (_manager == null || !_manager.IsServer || Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + _statusLogInterval;
            Debug.Log("[Net] connected clients=" + _manager.ConnectedClientsIds.Count);
        }

        private void OnDestroy()
        {
            MenuNavigator.ReturningToMenu -= HandleReturningToMenu;

            if (_lobby != null)
            {
                Application.quitting -= LeaveLobbyOnQuit;
                Application.wantsToQuit -= HoldQuitUntilServerLobbyClosed;
                CloseServerLobby();
                _lobby.Removed -= HandleLobbyRemoved;
                _lobby.Dispose();
                _lobby = null;
                _lobbyLauncher = null;
                _serverLobby = null;
            }

            Application.quitting -= TellClientsServerStopping;
            Application.wantsToQuit -= HandleWantsToQuit;
            if (_manager == null)
            {
                return;
            }

            // エディタで Play Mode を止めたときやシーンを替えたときも、クライアントへ止まると知らせる
            TellClientsServerStopping();
            _manager.OnServerStarted -= HandleServerStarted;
            _manager.OnClientConnectedCallback -= HandleClientConnected;
            _manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _manager.OnTransportFailure -= HandleTransportFailure;
        }

        private void HandleServerStarted()
        {
            Debug.Log("[Net] server listening on " + _options.Endpoint);
            SpawnMatch();

            // 待ち受けていないサーバーをクライアントに見つけさせないよう、試合を始められてから登録する
            if (!_failed && _serverLobby != null)
            {
                _serverLobby.Begin(Time.unscaledTime, HandleServerLobbyFailed);
            }
        }

        /// <summary>試合の進行役をサーバーで生成して Spawn する。クライアントには Netcode が複製を作る。</summary>
        private void SpawnMatch()
        {
            GameObject instance = null;
            try
            {
                instance = Instantiate(_matchPrefab);
                instance.name = _matchPrefab.name;

                // 試合は Spawn の中で始まる (OnNetworkSpawn) ので、Bot の数はその前に渡す
                instance.GetComponent<NetTeamDeathmatch>().LaunchBots = _options.Bots;
                instance.GetComponent<NetworkObject>().Spawn(true);
            }
            catch (Exception e)
            {
                // 起動処理の外側の境界なので、何が飛んできてもログと終了コードに変える
                Debug.LogException(e);
                if (instance != null)
                {
                    Destroy(instance);
                }

                Fail(ExitStartFailed, "[Net] failed to spawn the match " + _matchPrefab.name);
                return;
            }

            var match = instance.GetComponent<NetTeamDeathmatch>();
            if (!match.IsRunningOnServer)
            {
                // 設定やシーンの誤りは NetTeamDeathmatch が理由をエラーログに出している
                Fail(ExitMisconfigured, "[Net] the match could not start; see the [Match] error above");
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_manager.IsServer)
            {
                Debug.Log("[Net] client connected id=" + clientId + " clients=" + _manager.ConnectedClientsIds.Count);
                if (clientId != NetworkManager.ServerClientId)
                {
                    _remoteClients.Add(clientId);
                    _serverLobby?.SetPlayers(_remoteClients.Count);
                }

                return;
            }

            _connectedToServer = true;
            Debug.Log("[Net] client connected id=" + clientId + " (local) server=" + _options.Endpoint);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var reason = string.IsNullOrEmpty(_manager.DisconnectReason) ? "" : " reason=" + _manager.DisconnectReason;

            if (_manager.IsServer)
            {
                Debug.Log("[Net] client disconnected id=" + clientId + reason);
                // 切断のコールバックの時点で ConnectedClientsIds から外れているかは Netcode の版で違うので、自分で数える
                if (_remoteClients.Remove(clientId))
                {
                    _serverLobby?.SetPlayers(_remoteClients.Count);
                }

                return;
            }

            // メニューへ戻るために自分で切ったときの通知なので、失敗としては扱わない
            if (_returningToMenu)
            {
                return;
            }

            // クライアント側では自分の切断 (接続失敗・承認で断られたのを含む) として呼ばれる
            var notice = NetworkSetup.ClassifyDisconnect(_manager, _connectedToServer);
            if (!_connectedToServer)
            {
                Fail(ExitConnectFailed, "[Net] failed to connect to " + _options.Endpoint + " " + notice.Describe(), notice);
                return;
            }

            _connectedToServer = false;
            _failed = true;
            Debug.Log("[Net] disconnected from server id=" + clientId + " " + notice.Describe());
            // Host がいなくなったロビーに残っても意味が無い
            _lobby?.LeaveLobby();
            QuitIfHeadlessPlayer(ExitDisconnected);
            ReturnToMenuAfterDisconnect(notice);
        }

        private void HandleTransportFailure()
        {
            Fail(ExitTransportFailure, "[Net] transport failure (" + _options + ")");
        }

        /// <param name="exitCode">batchmode のプレイヤーで終了するときの終了コード。</param>
        /// <param name="message">エラーログに出す文。</param>
        /// <param name="notice">
        /// サーバーとの接続を失ったことによる失敗なら、その理由。あればどこから来たかによらずメニューへ戻り、理由の文を出す。
        /// </param>
        private void Fail(int exitCode, string message, DisconnectNotice notice = null)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            Debug.LogError(message);

            if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress)
            {
                TellClientsServerStopping();
                _manager.Shutdown();
            }

            // 試合を始められなかったので、作った・入ったロビーを残さない
            CloseServerLobby();
            _lobby?.LeaveLobby();
            QuitIfHeadlessPlayer(exitCode);
            if (notice != null)
            {
                ReturnToMenuAfterDisconnect(notice);
                return;
            }

            ReturnToMenuIfStartedThere(message);
        }

        /// <summary>
        /// メニューの「ロビーを作る／入る」から来たときだけ、理由を添えてメニューへ戻る。
        /// 起動引数で来たときは今までどおりこのシーンに留まる (batchmode では先に終了している)。
        /// </summary>
        private void ReturnToMenuIfStartedThere(string reason)
        {
            if (!_startedFromMenu)
            {
                return;
            }

            ReturnToMenu(reason);
        }

        /// <summary>
        /// サーバーとの接続を失ったクライアントを、理由の文を添えてメニューへ戻す。
        /// 起動引数でつないだときも、残っても遊べないので戻す (batchmode では先に終了している)。
        /// </summary>
        private void ReturnToMenuAfterDisconnect(DisconnectNotice notice)
        {
            ReturnToMenu(notice.Message);
        }

        private void ReturnToMenu(string notice)
        {
            if (_returningToMenu
                || NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            // メニューがビルドに無ければ MenuNavigator がエラーログを出し、このシーンに留まる
            MenuNavigator.ReturnToMenu(notice);
        }

        /// <summary>
        /// サーバー (Host を含む) が止まる前に、つないでいるクライアントへ <see cref="DisconnectNotice.ServerStoppedReason"/> を送って切る。
        /// 送らずに止めると、クライアントは理由が分からずタイムアウトとして扱う。1 度だけ送る。
        /// </summary>
        private void TellClientsServerStopping()
        {
            if (_toldClientsServerStopping || _manager == null)
            {
                return;
            }

            int count;
            try
            {
                count = NetworkSetup.DisconnectRemoteClients(_manager, DisconnectNotice.ServerStoppedReason);
            }
            catch (Exception e)
            {
                // 止める途中なので、知らせられなくても止めるのは続ける (クライアントはタイムアウトとして扱う)
                Debug.LogException(e);
                return;
            }

            _toldClientsServerStopping = true;
            if (count > 0)
            {
                Debug.Log("[Net] server stopping; told " + count + " client(s) and disconnected them");
            }
        }

        /// <summary>
        /// メニューへ戻る直前に呼ばれる。NetworkManager は DontDestroyOnLoad でシーンをまたいで残り、次に NetTest を読んだときの
        /// NetBootstrap が重複として消えてしまうので、接続を切ってこの GameObject ごと消す。ロビーは OnDestroy の Dispose で出る。
        /// </summary>
        private void HandleReturningToMenu()
        {
            if (_returningToMenu)
            {
                return;
            }

            _returningToMenu = true;
            _failed = true;
            Debug.Log("[Net] leaving for the menu; shutting down the network");
            if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress)
            {
                TellClientsServerStopping();
                _manager.Shutdown();
            }

            Destroy(gameObject);
        }

        /// <summary>batchmode のプレイヤーと専用サーバーだけ終了する。エディタでは Play Mode を止めずにログだけ残す。</summary>
        private static void QuitIfHeadlessPlayer(int exitCode)
        {
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, NetStartup.IsDedicatedServerBuild))
            {
                _requestedExitCode = exitCode;
                Application.Quit(exitCode);
            }
        }
    }
}
