using System;

namespace Shooter.Core
{
    public sealed class StaminaSpec
    {
        public const float DefaultMax = 100f;
        public const float DefaultSprintCostPerSecond = 20f;
        public const float DefaultSlideCost = 20f;
        public const float DefaultDodgeCost = 25f;
        public const float DefaultRegenPerSecond = 25f;
        public const float DefaultRegenDelaySeconds = 1f;
        public const float DefaultRecoverAmount = 30f;

        public static StaminaSpec Default { get; } = new StaminaSpec();

        public float Max { get; }

        public float SprintCostPerSecond { get; }

        public float SlideCost { get; }

        public float DodgeCost { get; }

        public float RegenPerSecond { get; }

        // 使うたびに数え直す。走りやめた直後や回避の直後に、すぐ戻り始めないようにするため。
        public float RegenDelaySeconds { get; }

        // 尽きたら 0 より上に戻っただけでは使わせず、ここまで戻ってから使わせる。0 を超えた途端に走り出しては尽きるのを毎 tick 繰り返さないため。
        public float RecoverAmount { get; }

        public StaminaSpec(
            float max = DefaultMax,
            float sprintCostPerSecond = DefaultSprintCostPerSecond,
            float slideCost = DefaultSlideCost,
            float dodgeCost = DefaultDodgeCost,
            float regenPerSecond = DefaultRegenPerSecond,
            float regenDelaySeconds = DefaultRegenDelaySeconds,
            float recoverAmount = DefaultRecoverAmount)
        {
            RequirePositive(max, nameof(max));
            RequireNonNegative(sprintCostPerSecond, nameof(sprintCostPerSecond));
            RequireUpToMax(slideCost, max, nameof(slideCost));
            RequireUpToMax(dodgeCost, max, nameof(dodgeCost));
            RequirePositive(regenPerSecond, nameof(regenPerSecond));
            RequireNonNegative(regenDelaySeconds, nameof(regenDelaySeconds));

            if (!(recoverAmount > 0f) || recoverAmount > max)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverAmount), recoverAmount, "recoverAmount must be greater than 0 and at most max");
            }

            Max = max;
            SprintCostPerSecond = sprintCostPerSecond;
            SlideCost = slideCost;
            DodgeCost = dodgeCost;
            RegenPerSecond = regenPerSecond;
            RegenDelaySeconds = regenDelaySeconds;
            RecoverAmount = recoverAmount;
        }

        public float CostOf(StaminaUse use)
        {
            switch (use)
            {
                case StaminaUse.Slide:
                    return SlideCost;
                case StaminaUse.Dodge:
                    return DodgeCost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(use), use, "use is not a defined StaminaUse");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }

        // max を超える一回の消費は満タンでも払えず、その動きが二度と使えなくなるので弾く。
        private static void RequireUpToMax(float value, float max, string name)
        {
            if (!(value >= 0f) || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and max");
            }
        }
    }
}
