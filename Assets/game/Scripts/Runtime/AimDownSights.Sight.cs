using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class AimDownSights
    {
        private ViewRig _viewRig;

        /// <summary>今の視点。null なら同じ GameObject の <see cref="ViewRig"/> の視点 (無ければ一人称)。</summary>
        public Func<ViewMode> ViewModeSource { get; set; }

        /// <summary>今の武器の照準器。武器が無ければ <see cref="SightSpec.Default"/>。</summary>
        public SightSpec Sight => WeaponSource?.Invoke()?.Aim.Sight ?? SightSpec.Default;

        /// <summary>
        /// 最後の <see cref="Tick(bool, bool, InputDeviceKind, float)"/> (か <see cref="ResetView"/>) での照準器の出し方。
        /// HUD がスコープの縁と照準を、<see cref="ViewRig"/> が三人称のカメラを目へ寄せる割合を、ビューモデルが隠すかを読む。
        /// </summary>
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
