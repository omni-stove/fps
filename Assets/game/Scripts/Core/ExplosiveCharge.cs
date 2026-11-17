using System;

namespace Shooter.Core
{
    public sealed class ExplosiveCharge
    {
        public ExplosiveCharge(ExplosivePropSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Health = spec.MaxHealth;
        }

        public ExplosivePropSpec Spec { get; }

        public int Health { get; private set; }

        public bool IsTriggered { get; private set; }

        public bool IsBurning => IsTriggered && !HasDetonated && Spec.Burns;

        public bool IsChained { get; private set; }

        public bool HasDetonated { get; private set; }

        public float RemainingSeconds { get; private set; }

        // 待つ秒数が 0 でもここでは爆発させず、爆発は Tick だけで起こす。
        public ExplosiveChargeStep ApplyDamage(int damage, bool chained)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (IsTriggered || damage == 0)
            {
                return ExplosiveChargeStep.None;
            }

            Health = Math.Max(0, Health - damage);
            if (Health > 0)
            {
                return ExplosiveChargeStep.Damaged;
            }

            IsTriggered = true;
            IsChained = chained;
            RemainingSeconds = Spec.DetonationDelay(chained);
            return Spec.Burns ? ExplosiveChargeStep.Ignited : ExplosiveChargeStep.Triggered;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsTriggered || HasDetonated)
            {
                return false;
            }

            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            RemainingSeconds = 0f;
            HasDetonated = true;
            return true;
        }
    }
}
