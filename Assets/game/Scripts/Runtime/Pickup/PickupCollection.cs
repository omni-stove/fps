using Shooter.Core;

namespace Shooter.Runtime
{
    public readonly struct PickupCollection
    {
        public PickupCollection(Pickup pickup, Damageable collector, PickupSpec spec, PickupOutcome outcome)
        {
            Pickup = pickup;
            Collector = collector;
            Spec = spec;
            Outcome = outcome;
        }

        public Pickup Pickup { get; }

        public Damageable Collector { get; }

        public PickupSpec Spec { get; }

        public PickupOutcome Outcome { get; }
    }
}
