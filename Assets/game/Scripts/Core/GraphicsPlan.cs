using System;

namespace Shooter.Core
{
    // QualitySettings・Screen・Application へ入れる値を、エンジンに触れずに決める。何をどの順に入れるかは AppliedGraphicsSettings が持つ。
    public readonly struct GraphicsPlan
    {
        // QualitySettings を触らない、という意味。
        public const int KeepQualityLevel = -1;

        // Application.targetFrameRate の「機器の既定のまま」。
        public const int PlatformFrameRate = -1;

        private GraphicsPlan(int qualityLevel, bool qualityClamped, int vSyncCount, int targetFrameRate, bool setsResolution, ScreenResolution resolution)
        {
            QualityLevel = qualityLevel;
            QualityClamped = qualityClamped;
            VSyncCount = vSyncCount;
            TargetFrameRate = targetFrameRate;
            SetsResolution = setsResolution;
            Resolution = resolution;
        }

        public int QualityLevel { get; }

        // 保存した段階がこのプロジェクトの段階の数を越えていて、最も高い段階に下げたか。
        public bool QualityClamped { get; }

        public int VSyncCount { get; }

        public int TargetFrameRate { get; }

        public bool SetsResolution { get; }

        public ScreenResolution Resolution { get; }

        // projectQualityLevel は起動したときの段階。ProjectQualityLevel に戻したときに、途中で選んだ段階のまま残さないため。
        // resolutionOverridden は、この起動中に解像度を変えたか。画面の解像度のままなら、起動時のウィンドウの大きさを変えないため。
        public static GraphicsPlan For(
            GraphicsSettings settings,
            bool headless,
            int qualityLevelCount,
            int projectQualityLevel,
            int headlessFrameRate,
            ScreenResolution display,
            bool resolutionOverridden)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (qualityLevelCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(qualityLevelCount), qualityLevelCount, "the number of quality levels cannot be negative");
            }

            if (headlessFrameRate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(headlessFrameRate), headlessFrameRate, "the headless frame rate must be at least 1");
            }

            // 画面の無いクライアントは描かないので画質と解像度は触らず、上限が無くても CPU を回し切らないよう headlessFrameRate で止める。
            if (headless)
            {
                var rate = settings.HasFrameRateLimit ? Math.Min(settings.FrameRateLimit, headlessFrameRate) : headlessFrameRate;
                return new GraphicsPlan(KeepQualityLevel, false, 0, rate, false, ScreenResolution.Native);
            }

            var quality = KeepQualityLevel;
            var clamped = false;
            if (qualityLevelCount > 0)
            {
                if (settings.QualityLevel == GraphicsSettings.ProjectQualityLevel)
                {
                    quality = Math.Max(0, Math.Min(qualityLevelCount - 1, projectQualityLevel));
                }
                else
                {
                    clamped = settings.QualityLevel >= qualityLevelCount;
                    quality = Math.Min(settings.QualityLevel, qualityLevelCount - 1);
                }
            }

            var target = settings.HasFrameRateLimit ? settings.FrameRateLimit : PlatformFrameRate;
            var setsResolution = !settings.Resolution.IsNative || (resolutionOverridden && !display.IsNative);
            var resolution = settings.Resolution.IsNative ? display : settings.Resolution;
            return new GraphicsPlan(quality, clamped, settings.VSync ? 1 : 0, target, setsResolution, setsResolution ? resolution : ScreenResolution.Native);
        }
    }
}
