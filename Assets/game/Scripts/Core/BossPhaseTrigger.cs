using System;
using System.Globalization;

namespace Fps.Core
{
    public sealed class BossPhaseTrigger
    {
        private readonly Func<BossPhaseContext, bool> _condition;

        private BossPhaseTrigger(float? healthFraction, string description, Func<BossPhaseContext, bool> condition)
        {
            HealthFraction = healthFraction;
            Description = description;
            _condition = condition;
        }

        public float? HealthFraction { get; }

        public string Description { get; }

        public static BossPhaseTrigger HealthAtOrBelow(float fraction)
        {
            if (!(fraction > 0f) || fraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "fraction must be greater than 0 and at most 1");
            }

            return new BossPhaseTrigger(
                fraction,
                "health<=" + fraction.ToString("0.###", CultureInfo.InvariantCulture),
                context => context.HealthFraction <= fraction);
        }

        public static BossPhaseTrigger When(string description, Func<BossPhaseContext, bool> condition)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("description must not be empty", nameof(description));
            }

            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            return new BossPhaseTrigger(null, description, condition);
        }

        public bool IsMet(BossPhaseContext context)
        {
            return _condition(context);
        }

        public override string ToString()
        {
            return Description;
        }
    }

    public readonly struct BossPhaseContext
    {
        public BossPhaseContext(float healthFraction, float secondsInPhase, int phaseIndex)
        {
            HealthFraction = healthFraction;
            SecondsInPhase = secondsInPhase;
            PhaseIndex = phaseIndex;
        }

        public float HealthFraction { get; }

        public float SecondsInPhase { get; }

        public int PhaseIndex { get; }
    }
}
