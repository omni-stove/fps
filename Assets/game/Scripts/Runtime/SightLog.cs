using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SightLog
    {
        public static string Format(SightView view)
        {
            var reticle = view.ShowsReticle ? (view.ReticleId.Length > 0 ? view.ReticleId : "default") : "-";
            return $"{AimDownSights.LogTag} sight aimedIn={Bool(view.AimedIn)} overlay={Bool(view.ScopeOverlay)} reticle={reticle} eye={Bool(view.EyeBlend > 0f)} vision={view.VisionMode}";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
