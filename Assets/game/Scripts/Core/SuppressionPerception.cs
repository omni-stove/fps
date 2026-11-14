using System;

namespace Fps.Core
{
    public sealed class SuppressionPerception : IPerceptionContributor
    {
        public SuppressionPerception(SuppressionMeter meter)
        {
            Meter = meter ?? throw new ArgumentNullException(nameof(meter));
        }

        public SuppressionMeter Meter { get; }

        public EnemyPerception Contribute(EnemyPerception perception)
        {
            return perception.WithSuppression(Meter.Level, Meter.IsSuppressed);
        }
    }
}
