using Fps.Core;

namespace Fps.Runtime
{
    /// <summary><see cref="Pickup.Collected"/> で渡す、1 回拾われたときの情報。</summary>
    public readonly struct PickupCollection
    {
        public PickupCollection(Pickup pickup, Damageable collector, PickupSpec spec, PickupOutcome outcome)
        {
            Pickup = pickup;
            Collector = collector;
            Spec = spec;
            Outcome = outcome;
        }

        /// <summary>拾われた物。<see cref="Pickup.DestroyOnCollect"/> なら受け取った後に破棄される。</summary>
        public Pickup Pickup { get; }

        /// <summary>拾った人。</summary>
        public Damageable Collector { get; }

        /// <summary>拾われた物の中身。</summary>
        public PickupSpec Spec { get; }

        /// <summary>拾った人に反映したこと。</summary>
        public PickupOutcome Outcome { get; }
    }
}
