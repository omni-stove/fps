using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // HTTP の送り先は Flush の間隔で送るので、毎フレームの更新を受ける持ち主が要る。時間は止めている間も進む unscaled で数える。
    [DisallowMultipleComponent]
    public sealed class TelemetryHost : MonoBehaviour
    {
        public const string ObjectName = "TelemetryHost";

        private static TelemetryHost _instance;

        private bool _listeningForQuit;

        public static TelemetryHost Instance => _instance;

        public ClientTelemetry Telemetry { get; private set; }

        // ドメインの再読み込みを切った Play で、前の Play やテストのホストへの参照が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _instance = null;
        }

        // 専用サーバーの記録は ServerTelemetry が持つ。ここで足すと送り元が client の行が混ざるため、サーバーのビルドでは置かない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnLoad()
        {
            if (NetStartup.IsDedicatedServerBuild)
            {
                return;
            }

            Install();
        }

        public static TelemetryHost Install()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject(ObjectName);
            // Edit Mode では DontDestroyOnLoad が例外を投げるので、Play の間だけ常駐させる
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            var host = go.AddComponent<TelemetryHost>();
            _instance = host;
            // Edit Mode では OnEnable が呼ばれないので、入口から記録を始めておく。Play では OnEnable と重なっても Begin が二度目を無視する
            host.Begin();
            return host;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            if (_instance != this)
            {
                return;
            }

            Begin();
        }

        private void OnDisable()
        {
            StopListeningForQuit();
            Telemetry?.Detach();
        }

        private void OnDestroy()
        {
            StopListeningForQuit();
            Telemetry?.Dispose();
            Telemetry = null;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            Telemetry?.Tick(Time.unscaledDeltaTime);
        }

        private void Begin()
        {
            if (Telemetry == null)
            {
                Telemetry = ClientTelemetry.CreateDefault();
            }

            Telemetry.Attach();
            if (!_listeningForQuit)
            {
                Application.quitting += HandleQuitting;
                _listeningForQuit = true;
            }
        }

        private void StopListeningForQuit()
        {
            if (!_listeningForQuit)
            {
                return;
            }

            Application.quitting -= HandleQuitting;
            _listeningForQuit = false;
        }

        // 終了では OnDestroy より前に送り切っておく。機種によっては OnDestroy を待たずにプロセスが終わるため
        private void HandleQuitting()
        {
            StopListeningForQuit();
            Telemetry?.Dispose();
            Telemetry = null;
        }
    }
}
