using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class AimDownSights
    {
        private ViewRig _viewRig;

        public Func<ViewMode> ViewModeSource { get; set; }

        public SightSpec Sight => WeaponSource?.Invoke()?.Aim.Sight ?? SightSpec.Default;

        public SightView SightView { get; private set; }

        private ViewMode CurrentViewMode()
        {
            if (ViewModeSource != null)
            {
                return ViewModeSource();
            }

            // 組み立てる側が ViewRig を後から付けることがあるので、見つかるまで探し直す。
            if (_viewRig == null)
            {
                _viewRig = GetComponent<ViewRig>();
            }

            return _viewRig != null ? _viewRig.Mode : ViewMode.FirstPerson;
        }

        private void StepSight()
        {
            var next = Shooter.Core.SightView.Of(Sight, CurrentViewMode(), Progress);
            var previous = SightView;
            SightView = next;
            if (next.AimedIn != previous.AimedIn
                || next.ScopeOverlay != previous.ScopeOverlay
                || next.ShowsReticle != previous.ShowsReticle
                || !string.Equals(next.ReticleId, previous.ReticleId, StringComparison.Ordinal)
                || (next.EyeBlend > 0f) != (previous.EyeBlend > 0f))
            {
                Debug.Log(SightLog.Format(next), this);
            }
        }
    }
}
