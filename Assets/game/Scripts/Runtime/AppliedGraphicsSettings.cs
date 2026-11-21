using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class AppliedGraphicsSettings
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static GraphicsSettings _current;
        private static int? _projectQualityLevel;
        private static bool _resolutionOverridden;

        public static IGraphicsSettingsSink Sink { get; } = new AppliedSink();

        public static GraphicsSettings Current
        {
            get
            {
                if (_current == null)
                {
                    Apply(new GraphicsSettingsStore().Load());
                }

                return _current;
            }
        }

        public static bool IsHeadless => NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, NetStartup.IsDedicatedServerBuild);

        public static void Apply(GraphicsSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _current = settings;

            // 専用サーバーは自分の刻みで回すので、プレイヤー向けの fps 上限や画質を入れない。
            if (NetStartup.IsDedicatedServerBuild)
            {
                return;
            }

            var headless = IsHeadless;
            var names = QualitySettings.names;
            var plan = PlanFor(settings, headless, names.Length);

            if (plan.QualityClamped)
            {
                Debug.LogWarning(GraphicsSettingsLog.QualityClamped(settings.QualityLevel, plan.QualityLevel, names.Length));
            }

            // 画質の段階はそれぞれ VSync を持っていて、段階を変えるとそれで上書きされるので、VSync は段階の後に入れる。
            if (plan.QualityLevel != GraphicsPlan.KeepQualityLevel && plan.QualityLevel != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(plan.QualityLevel, true);
            }

            QualitySettings.vSyncCount = plan.VSyncCount;
            Application.targetFrameRate = plan.TargetFrameRate;

            if (plan.SetsResolution)
            {
                ApplyResolution(plan.Resolution);
            }

            Debug.Log(GraphicsSettingsLog.Applied(settings, plan, headless));
        }

        // 画質や解像度は入れ直さず、fps の上限と VSync だけを今の設定から入れ直す。オンラインに入るときに NetV2Client が呼ぶ。
        public static void ApplyFrameRate()
        {
            var settings = Current;
            if (NetStartup.IsDedicatedServerBuild)
            {
                return;
            }

            var plan = PlanFor(settings, IsHeadless, QualitySettings.names.Length);
            QualitySettings.vSyncCount = plan.VSyncCount;
            Application.targetFrameRate = plan.TargetFrameRate;
        }

        private static GraphicsPlan PlanFor(GraphicsSettings settings, bool headless, int qualityLevelCount)
        {
            _projectQualityLevel ??= QualitySettings.GetQualityLevel();
            return GraphicsPlan.For(
                settings,
                headless,
                qualityLevelCount,
                _projectQualityLevel.Value,
                KitTuning.Graphics.HeadlessFrameRate,
                DisplayResolution(),
                _resolutionOverridden);
        }

        private static void ApplyResolution(ScreenResolution resolution)
        {
            try
            {
                Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreenMode);
                _resolutionOverridden = true;
            }
            catch (Exception e)
            {
                // 解像度が入らなくても、画質と fps の上限はもう効いているので止めない。
                Debug.LogError(GraphicsSettingsLog.ApplyFailed("resolution " + resolution, e.Message));
                Debug.LogException(e);
            }
        }

        private static ScreenResolution DisplayResolution()
        {
            var display = Screen.currentResolution;
            return ScreenResolution.IsValid(display.width, display.height)
                ? new ScreenResolution(display.width, display.height)
                : ScreenResolution.Native;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _current = null;
            _projectQualityLevel = null;
            _resolutionOverridden = false;
        }

        // メニューで設定画面を開く前から、保存した画質と fps の上限で動かす。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnPlay()
        {
            _ = Current;
        }

        private sealed class AppliedSink : IGraphicsSettingsSink
        {
            public GraphicsSettings Current => AppliedGraphicsSettings.Current;

            public void Apply(GraphicsSettings settings)
            {
                AppliedGraphicsSettings.Apply(settings);
            }
        }
    }
}
