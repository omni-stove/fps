using System;

namespace Shooter.Core
{
    public readonly struct SightView : IEquatable<SightView>
    {
        public static SightView None => default;

        private readonly string _reticleId;

        private SightView(bool throughSight, bool aimedIn, bool scopeOverlay, string reticleId, float eyeBlend, VisionMode visionMode)
        {
            ThroughSight = throughSight;
            AimedIn = aimedIn;
            ScopeOverlay = scopeOverlay;
            _reticleId = reticleId;
            EyeBlend = eyeBlend;
            VisionMode = visionMode;
        }

        public bool ThroughSight { get; }

        public bool AimedIn { get; }

        public bool ScopeOverlay { get; }

        public string ReticleId => _reticleId ?? string.Empty;

        // 縁を出すスコープは見た目のキーが無くても照準が要る (縁だけでは狙う点が分からない)。
        public bool ShowsReticle => AimedIn && (ReticleId.Length > 0 || ScopeOverlay);

        public bool HidesViewmodel => ScopeOverlay;

        public float EyeBlend { get; }

        // 寄せ始めからカメラが体の中を通るので、寄り切るのを待たずに隠す。
        public bool HidesBody => EyeBlend > 0f;

        public VisionMode VisionMode { get; }

        public static SightView Of(SightSpec sight, ViewMode view, float adsProgress, AdsEyeView eyeViewRule = AdsEyeView.PerWeapon)
        {
            if (view != ViewMode.FirstPerson && view != ViewMode.ThirdPerson)
            {
                throw new ArgumentOutOfRangeException(nameof(view), view, "unknown view mode");
            }

            if (!Enum.IsDefined(typeof(AdsEyeView), eyeViewRule))
            {
                throw new ArgumentOutOfRangeException(nameof(eyeViewRule), eyeViewRule, "eyeViewRule is not a defined AdsEyeView");
            }

            sight = sight ?? SightSpec.Default;
            var progress = float.IsNaN(adsProgress) ? 0f : Math.Max(0f, Math.Min(1f, adsProgress));
            var eyeView = view == ViewMode.ThirdPerson && EyeViewInThirdPerson(sight, eyeViewRule);
            var throughSight = view == ViewMode.FirstPerson || eyeView;
            var aimedIn = throughSight && progress >= sight.AimedInProgress;
            return new SightView(
                throughSight,
                aimedIn,
                aimedIn && sight.ScopeOverlay,
                aimedIn ? sight.ReticleId : string.Empty,
                eyeView ? progress : 0f,
                aimedIn ? sight.VisionMode : VisionMode.None);
        }

        private static bool EyeViewInThirdPerson(SightSpec sight, AdsEyeView rule)
        {
            switch (rule)
            {
                case AdsEyeView.Always: return true;
                case AdsEyeView.Never: return false;
                default: return sight.EyeViewInThirdPerson;
            }
        }

        public bool Equals(SightView other)
        {
            return ThroughSight == other.ThroughSight
                && AimedIn == other.AimedIn
                && ScopeOverlay == other.ScopeOverlay
                && string.Equals(ReticleId, other.ReticleId, StringComparison.Ordinal)
                && EyeBlend.Equals(other.EyeBlend)
                && VisionMode == other.VisionMode;
        }

        public static bool operator ==(SightView left, SightView right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SightView left, SightView right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is SightView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ThroughSight.GetHashCode();
                hash = (hash * 31) + AimedIn.GetHashCode();
                hash = (hash * 31) + ScopeOverlay.GetHashCode();
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ReticleId);
                hash = (hash * 31) + EyeBlend.GetHashCode();
                return (hash * 31) + (int)VisionMode;
            }
        }
    }
}
