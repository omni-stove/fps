using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class GraphicsSettingsLog
    {
        public const string Tag = "[Graphics]";

        public static string Problem(string key, string problem)
        {
            return Tag + " graphics settings '" + key + "': " + problem + "; using the default or the nearest valid value";
        }

        public static string Applied(GraphicsSettings settings, GraphicsPlan plan, bool headless)
        {
            return Tag + " applied " + settings
                + " (headless=" + (headless ? "yes" : "no")
                + " qualityLevel=" + (plan.QualityLevel == GraphicsPlan.KeepQualityLevel ? "kept" : Format(plan.QualityLevel))
                + " vSyncCount=" + Format(plan.VSyncCount)
                + " targetFrameRate=" + Format(plan.TargetFrameRate)
                + " resolution=" + (plan.SetsResolution ? plan.Resolution.ToString() : "kept") + ")";
        }

        public static string QualityClamped(int saved, int used, int count)
        {
            return Tag + " quality level " + Format(saved) + " is not one of this project's " + Format(count)
                + " levels (Project Settings > Quality); using " + Format(used);
        }

        public static string ApplyFailed(string what, string message)
        {
            return Tag + " could not apply the " + what + ": " + message;
        }

        public static string SaveFailed(string key, string message)
        {
            return Tag + " could not save the graphics settings to '" + key + "': " + message;
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
