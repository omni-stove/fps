using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // 同意は設定の画面で変わるので、作ったときの値で決め打ちせず、AppliedControlSettings.Changed で記録係に伝え続ける。
    public sealed class ClientTelemetry : IDisposable
    {
        private bool _disposed;

        public ClientTelemetry(TelemetryRecorder recorder, Func<float> clock)
        {
            Recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            Crashes = new CrashCapture(recorder);
            // 場面の名前は要約の見出しとして書くだけで、中身を引くのには使わない
            Matches = new MatchSummaryReporter(recorder, clock, map: () => SceneManager.GetActiveScene().name);
        }

        public TelemetryRecorder Recorder { get; }

        public CrashCapture Crashes { get; }

        public MatchSummaryReporter Matches { get; }

        public bool IsAttached { get; private set; }

        public static string DefaultFolder => Application.persistentDataPath;

        public static TelemetrySpec LoadSpec()
        {
            return KitTuning.Load(c => c.TelemetryTuning, TelemetryJson.ResourcePath, TelemetryJson.Parse, TelemetrySpec.Default);
        }

        public static ClientTelemetry CreateDefault()
        {
            return Create(LoadSpec(), DefaultFolder, AppliedControlSettings.Current.TelemetryConsent);
        }

        public static ClientTelemetry Create(TelemetrySpec spec, string folder, bool consent)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var origin = new TelemetryOrigin(
                TelemetryOrigin.ClientSource, TelemetryOrigin.NewSession(), Application.version, Application.platform.ToString());
            // ゲームの乱数 (UnityEngine.Random) の並びを変えないよう、別の乱数で選ぶ
            var random = new System.Random();
            var recorder = new TelemetryRecorder(
                spec,
                origin,
                consent,
                () => TelemetrySinks.Create(spec, folder, Debug.LogWarning),
                () => DateTime.UtcNow,
                random.NextDouble,
                Debug.Log);
            return new ClientTelemetry(recorder, () => Time.realtimeSinceStartup);
        }

        public void Attach()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("the telemetry is already disposed");
            }

            if (IsAttached)
            {
                return;
            }

            Crashes.Attach();
            Matches.Attach();
            AppliedControlSettings.Changed += HandleSettingsChanged;
            IsAttached = true;
        }

        public void Detach()
        {
            if (!IsAttached)
            {
                return;
            }

            AppliedControlSettings.Changed -= HandleSettingsChanged;
            Matches.Detach();
            Crashes.Detach();
            IsAttached = false;
        }

        public void Tick(float deltaSeconds)
        {
            Recorder.Tick(deltaSeconds);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Detach();
            Recorder.Dispose();
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            Recorder.SetConsent(settings.TelemetryConsent);
        }
    }
}
