using System;

namespace Shooter.Core
{
    public sealed class TriggerSpec
    {
        public const FireMode DefaultFireMode = FireMode.Auto;

        public const float DefaultFireInterval = 0.1f;

        public static readonly TriggerSpec Default = new TriggerSpec();

        public TriggerSpec(
            FireMode fireMode = DefaultFireMode,
            float fireInterval = DefaultFireInterval,
            BurstSpec burst = null,
            ChargeSpec charge = null,
            SpinUpSpec spinUp = null,
            CycleSpec cycle = null)
        {
            if (fireMode != FireMode.Auto && fireMode != FireMode.Semi)
            {
                throw new ArgumentOutOfRangeException(nameof(fireMode), fireMode, "fireMode must be Auto or Semi");
            }

            FireMode = fireMode;
            FireInterval = SpecGuard.NonNegativeFinite(fireInterval, nameof(fireInterval));
            Burst = burst;
            Charge = charge;
            SpinUp = spinUp;
            Cycle = cycle;
        }

        public FireMode FireMode { get; }

        public float FireInterval { get; }

        public BurstSpec Burst { get; }

        public ChargeSpec Charge { get; }

        public SpinUpSpec SpinUp { get; }

        public CycleSpec Cycle { get; }

        public TriggerSpec WithFireInterval(float fireInterval)
        {
            return fireInterval == FireInterval
                ? this
                : new TriggerSpec(FireMode, fireInterval, Burst, Charge, SpinUp, Cycle);
        }
    }
}
