using System;

namespace Shooter.Core
{
    /// <summary>
    /// 照準器 (<see cref="SightSpec"/>) を、今の視点と覗き込みの進み具合で画面にどう出すか。
    /// 一人称か、三人称で目から覗く照準器なら照準器越しに見る (<see cref="ThroughSight"/>)。
    /// 照準器越しに構えきったら (<see cref="AimedIn"/>) 照準とスコープの縁を出し、縁を出す間は一人称の武器を隠す。
    /// 三人称で目から覗く照準器は、進み具合に合わせてカメラを目へ寄せ (<see cref="EyeBlend"/>)、寄せ始めたら自分の体を隠す。
    /// </summary>
    public readonly struct SightView : IEquatable<SightView>
    {
        public static SightView None => default;

        private readonly string _reticleId;

        private SightView(bool throughSight, bool aimedIn, bool scopeOverlay, string reticleId, float eyeBlend)
        {
            ThroughSight = throughSight;
            AimedIn = aimedIn;
            ScopeOverlay = scopeOverlay;
            _reticleId = reticleId;
            EyeBlend = eyeBlend;
        }

        public bool ThroughSight { get; }

        public bool AimedIn { get; }

        public bool ScopeOverlay { get; }

        /// <summary>出す照準の見た目のキー。照準を出さないときと、照準器が見た目を決めていないときは空。</summary>
        public string ReticleId => _reticleId ?? string.Empty;

        // 縁を出すスコープは見た目のキーが無くても照準が要る (縁だけでは狙う点が分からない)。
        public bool ShowsReticle => AimedIn && (ReticleId.Length > 0 || ScopeOverlay);

        public bool HidesViewmodel => ScopeOverlay;

        /// <summary>三人称のカメラを肩越し (0) から目 (1) へ寄せる割合。</summary>
        public float EyeBlend { get; }

        // 寄せ始めからカメラが体の中を通るので、寄り切るのを待たずに隠す。
        public bool HidesBody => EyeBlend > 0f;

        /// <param name="sight">今の武器の照準器。null なら <see cref="SightSpec.Default"/>。</param>
        /// <param name="view">今の視点。</param>
        /// <param name="adsProgress">覗き込みの進み具合。0〜1 に丸める。NaN は 0。</param>
        public static SightView Of(SightSpec sight, ViewMode view, float adsProgress)
        {
            if (view != ViewMode.FirstPerson && view != ViewMode.ThirdPerson)
            {
                throw new ArgumentOutOfRangeException(nameof(view), view, "unknown view mode");
            }

            sight = sight ?? SightSpec.Default;
            var progress = float.IsNaN(adsProgress) ? 0f : Math.Max(0f, Math.Min(1f, adsProgress));
            var eyeView = view == ViewMode.ThirdPerson && sight.EyeViewInThirdPerson;
            var throughSight = view == ViewMode.FirstPerson || eyeView;
            var aimedIn = throughSight && progress >= sight.AimedInProgress;
            return new SightView(
                throughSight,
                aimedIn,
                aimedIn && sight.ScopeOverlay,
                aimedIn ? sight.ReticleId : string.Empty,
                eyeView ? progress : 0f);
        }

        public bool Equals(SightView other)
        {
            return ThroughSight == other.ThroughSight
                && AimedIn == other.AimedIn
                && ScopeOverlay == other.ScopeOverlay
                && string.Equals(ReticleId, other.ReticleId, StringComparison.Ordinal)
                && EyeBlend.Equals(other.EyeBlend);
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
                return (hash * 31) + EyeBlend.GetHashCode();
            }
        }
    }
}
