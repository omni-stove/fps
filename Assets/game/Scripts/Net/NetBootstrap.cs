using System;
using System.Collections.Generic;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// コマンドライン引数 (<c>-mode host|server|client</c> / <c>-address</c> / <c>-port</c> /
    /// <c>-transport udp|eos</c> / <c>-eos-server</c>) に従って NetworkManager を組み立てて起動する。
    /// 引数が無ければ <see cref="DefaultMode"/> (既定は Host) の UDP で起動する。
    /// EOS を要求されても認証情報が無ければ UDP に切り替え、<see cref="CredentialsMissingLog"/> を出す。
    /// 失敗はすべて <c>[Net]</c> 付きのエラーログに出し、batchmode のプレイヤーでは終了コード付きで終了する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetBootstrap : MonoBehaviour
    {
        /// <summary>このコンポーネントを置くシーン。<c>Fps.Editor.NetSceneBuilder</c> が生成する。</summary>
        public const string ScenePath = "Assets/game/Scenes/NetTest.unity";

        public const int ExitDisconnected = 0;
        public const int ExitInvalidArguments = 2;
        public const int ExitMisconfigured = 3;
        public const int ExitStartFailed = 4;
        public const int ExitConnectFailed = 5;
        public const int ExitTransportFailure = 6;

        /// <summary>EOS を要求されたが認証情報が揃わず、UDP に切り替えたときのログ。</summary>
        public const string CredentialsMissingLog = "[Net] EOS credentials missing, using UDP";

        [SerializeField]
        [Tooltip("接続したクライアントごとに生成するプレハブ (NetworkObject 必須)")]
        private GameObject _playerPrefab;

        [SerializeField]
        [Tooltip("-mode が無いときの起動モード")]
        private NetLaunchMode _defaultMode = NetLaunchMode.Host;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("サーバーが接続人数をログに出す間隔 (秒)")]
        private float _statusLogInterval = 1f;

        private NetworkManager _manager;
        private NetLaunchOptions _options;
        private bool _connectedToServer;
        private bool _failed;
        private float _nextStatusLogTime;

        public GameObject PlayerPrefab
        {
            get => _playerPrefab;
            set => _playerPrefab = value;
        }

        public NetLaunchMode DefaultMode
        {
            get => _defaultMode;
            set => _defaultMode = value;
        }

        /// <summary>
        /// プレイヤーを <c>-mode</c> 付きで起動したとき、最初のシーンが NetTest でなければ NetTest へ切り替える。
        /// ふだんの起動 (引数なし) では何もしない。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadNetSceneWhenRequested()
        {
            if (Application.isEditor || !NetLaunchOptions.HasModeFlag(Environment.GetCommandLineArgs()))
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
                Debug.LogWarning("[Net] a NetworkManager already exists; destroying duplicate " + name);
                Destroy(gameObject);
                return;
            }

            // サーバーとクライアントを同じマシンで並べて動かすので、裏に回っても止めない
            Application.runInBackground = true;
            if (Application.isBatchMode)
            {
                // 描画しないと CPU を使い切るまで回るので、ティック (既定 30Hz) より十分速い程度に抑える
                Application.targetFrameRate = 60;
            }

            if (!NetLaunchOptions.TryParse(Environment.GetCommandLineArgs(), _defaultMode, out _options, out var error))
            {
                Fail(ExitInvalidArguments, "[Net] invalid arguments: " + error);
                return;
            }

            if (_playerPrefab == null)
            {
                Fail(ExitMisconfigured, "[Net] player prefab is not assigned on " + name + "; run Fps.Editor.NetSceneBuilder.Build");
                return;
            }

            var plan = PlanTransport(_options);
            if (plan == null)
            {
                return;
            }

            _options = plan.Options;

            try
            {
                _manager = NetworkSetup.Configure(gameObject, plan, _playerPrefab);
            }
            catch (ArgumentException e)
            {
                Fail(ExitMisconfigured, "[Net] cannot configure NetworkManager: " + e.Message);
                return;
            }
            catch (InvalidOperationException e)
            {
                Fail(ExitMisconfigured, "[Net] cannot configure NetworkManager: " + e.Message);
                return;
            }

            _manager.OnServerStarted += HandleServerStarted;
            _manager.OnClientConnectedCallback += HandleClientConnected;
            _manager.OnClientDisconnectCallback += HandleClientDisconnected;
            _manager.OnTransportFailure += HandleTransportFailure;
        }

        /// <summary>
        /// EOS を要求されていれば認証情報を読み、使う transport を <see cref="NetTransportPlan"/> に決めさせる。
        /// 認証情報のファイルが壊れていれば失敗として null を返す。
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

            return plan;
        }

        private void Start()
        {
            if (_failed || _manager == null)
            {
                return;
            }

            // StartHost は接続のコールバックを同期で呼ぶので、開始のログは先に出す
            Debug.Log("[Net] starting " + _options + " transport=" + NetLaunchOptions.TransportName(_options.Transport));
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
        }

        private void Update()
        {
            if (_manager == null || !_manager.IsServer || Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + _statusLogInterval;
            Debug.Log("[Net] connected clients=" + _manager.ConnectedClientsIds.Count);
        }

        private void OnDestroy()
        {
            if (_manager == null)
            {
                return;
            }

            _manager.OnServerStarted -= HandleServerStarted;
            _manager.OnClientConnectedCallback -= HandleClientConnected;
            _manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _manager.OnTransportFailure -= HandleTransportFailure;
        }

        private void HandleServerStarted()
        {
            Debug.Log("[Net] server listening on " + _options.Endpoint);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_manager.IsServer)
            {
                Debug.Log("[Net] client connected id=" + clientId + " clients=" + _manager.ConnectedClientsIds.Count);
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
                return;
            }

            // クライアント側では自分の切断 (接続失敗を含む) として呼ばれる
            if (!_connectedToServer)
            {
                Fail(ExitConnectFailed, "[Net] failed to connect to " + _options.Endpoint + reason);
                return;
            }

            _connectedToServer = false;
            Debug.Log("[Net] disconnected from server id=" + clientId + reason);
            QuitIfHeadlessPlayer(ExitDisconnected);
        }

        private void HandleTransportFailure()
        {
            Fail(ExitTransportFailure, "[Net] transport failure (" + _options + ")");
        }

        private void Fail(int exitCode, string message)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            Debug.LogError(message);

            if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress)
            {
                _manager.Shutdown();
            }

            QuitIfHeadlessPlayer(exitCode);
        }

        /// <summary>batchmode のプレイヤーだけ終了する。エディタでは Play Mode を止めずにログだけ残す。</summary>
        private static void QuitIfHeadlessPlayer(int exitCode)
        {
            if (!Application.isEditor && Application.isBatchMode)
            {
                Application.Quit(exitCode);
            }
        }
    }
}
