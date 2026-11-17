using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SightLog
    {
        /// <summary>例: <c>[View] sight aimedIn=true overlay=true reticle=mildot eye=true</c>。reticle は照準を出さないとき <c>-</c>。</summary>
        public static string Format(SightView view)
        {
            var reticle = view.ShowsReticle ? (view.ReticleId.Length > 0 ? view.ReticleId : "default") : "-";
            return $"{AimDownSights.LogTag} sight aimedIn={Bool(view.AimedIn)} overlay={Bool(view.ScopeOverlay)} reticle={reticle} eye={Bool(view.EyeBlend > 0f)}";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
