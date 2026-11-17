using System;

namespace Shooter.Core
{
    public sealed class SpinUpSpec
    {
        // 間隔の秒数でなく倍率で持つのは、TriggerSpec.FireInterval を変えたときに回り始めの間隔も一緒に動くようにするため。
        public SpinUpSpec(float spinUpSeconds, float startIntervalMultiplier, float spinDownSeconds)
        {
            SpinUpSeconds = SpecGuard.PositiveFinite(spinUpSeconds, nameof(spinUpSeconds));

            if (!(startIntervalMultiplier >= 1f) || float.IsInfinity(startIntervalMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startIntervalMultiplier), startIntervalMultiplier, "startIntervalMultiplier must be at least 1 and finite");
            }

            StartIntervalMultiplier = startIntervalMultiplier;
            SpinDownSeconds = SpecGuard.NonNegativeFinite(spinDownSeconds, nameof(spinDownSeconds));
        }

        public float SpinUpSeconds { get; }

        public float StartIntervalMultiplier { get; }

        public float SpinDownSeconds { get; }
    }
}
