using System;

namespace Shooter.Core
{
    public readonly struct ContextMeleeSituation
    {
        public ContextMeleeSituation(ContextMeleeConditions conditions, float distance)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must be a non-negative finite number");
            }

            Conditions = conditions;
            Distance = distance;
        }

        public ContextMeleeConditions Conditions { get; }

        // 振る者の中心から相手の体の表面まで (ContextMeleeMove.Reach と同じ測り方)。
        public float Distance { get; }

        public bool Has(ContextMeleeConditions conditions)
        {
            return (Conditions & conditions) == conditions;
        }
    }
}
