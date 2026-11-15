using System;

namespace Fps.Core
{
    public sealed class HeatSpec
    {
        public HeatSpec(float heatPerShot, float coolPerSecond, float coolDelaySeconds, float overheatSeconds)
        {
            if (!(heatPerShot > 0f && heatPerShot <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(heatPerShot), heatPerShot, "heatPerShot must be greater than 0 and at most 1");
            }

            HeatPerShot = heatPerShot;
            CoolPerSecond = SpecGuard.PositiveFinite(coolPerSecond, nameof(coolPerSecond));
            CoolDelaySeconds = SpecGuard.NonNegativeFinite(coolDelaySeconds, nameof(coolDelaySeconds));
            OverheatSeconds = SpecGuard.NonNegativeFinite(overheatSeconds, nameof(overheatSeconds));
        }

        public float HeatPerShot { get; }

        public float CoolPerSecond { get; }

        public float CoolDelaySeconds { get; }

        public float OverheatSeconds { get; }
    }
}
