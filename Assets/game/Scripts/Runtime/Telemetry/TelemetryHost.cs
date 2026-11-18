using UnityEngine;

namespace Shooter.Runtime
{
    // HTTP の送り先は Flush の間隔で送るので、毎フレームの更新を受ける持ち主が要る。時間は止めている間も進む unscaled で数える。
    [DisallowMultipleComponent]
    public sealed class TelemetryHost : MonoBehaviour
    {
        public ClientTelemetry Telemetry { get; private set; }

        private void OnEnable()
        {
            if (Telemetry == null)
            {
                Telemetry = ClientTelemetry.CreateDefault();
            }

            Telemetry.Attach();
            Application.quitting += HandleQuitting;
        }

        private void OnDisable()
        {
            Application.quitting -= HandleQuitting;
            Telemetry?.Detach();
        }

        private void OnDestroy()
        {
            Telemetry?.Dispose();
            Telemetry = null;
        }

        private void Update()
        {
            Telemetry?.Tick(Time.unscaledDeltaTime);
        }

        // 終了では OnDestroy より前に送り切っておく。機種によっては OnDestroy を待たずにプロセスが終わるため
        private void HandleQuitting()
        {
            Telemetry?.Dispose();
            Telemetry = null;
        }
    }
}
