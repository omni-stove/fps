using System;

namespace Shooter.Core
{
    // 時間を自分で読まず Tick で受けるのは、Unity の外 (dotnet のテスト) でも同じ計算を回すため。
    public sealed class StaminaMeter
    {
        private float _sinceSpend;

        public StaminaMeter(StaminaSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Refill();
        }

        public StaminaSpec Spec { get; }

        public float Current { get; private set; }

        public bool IsDepleted { get; private set; }

        public bool IsFull => Current >= Spec.Max;

        public bool CanSprint => !IsDepleted;

        public StaminaStatus Status => new StaminaStatus(Current, Spec.Max, IsDepleted);

        // 消費が 0 の動きは尽きていても断らない。払う物が無いのに止める理由が無いため。
        public bool CanAfford(float cost)
        {
            RequireCost(cost);
            return cost <= 0f || (!IsDepleted && Current >= cost);
        }

        public bool CanAfford(StaminaUse use)
        {
            return CanAfford(Spec.CostOf(use));
        }

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            Drain(cost);
            return true;
        }

        public bool TrySpend(StaminaUse use)
        {
            return TrySpend(Spec.CostOf(use));
        }

        // 尽きている間はダッシュを押していても減らさず戻す。止めた後もボタンを押したままの tick で、戻り始めを遅らせないため。
        public void Tick(float deltaSeconds, bool sprinting)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (sprinting && !IsDepleted && Spec.SprintCostPerSecond > 0f)
            {
                if (deltaSeconds > 0f)
                {
                    Drain(deltaSeconds * Spec.SprintCostPerSecond);
                }

                return;
            }

            // 1 回の Tick が待ちの終わりをまたいでも、またいだ後の分だけ戻す (フレームの長さで戻り方が変わらないように)。
            var waiting = Math.Max(0f, Spec.RegenDelaySeconds - _sinceSpend);
            var regenerating = Math.Max(0f, deltaSeconds - waiting);
            _sinceSpend = Math.Min(Spec.RegenDelaySeconds, _sinceSpend + deltaSeconds);
            Current = Math.Min(Spec.Max, Current + (regenerating * Spec.RegenPerSecond));

            if (IsDepleted && Current >= Spec.RecoverAmount)
            {
                IsDepleted = false;
            }
        }

        public void Refill()
        {
            Current = Spec.Max;
            IsDepleted = false;
            _sinceSpend = Spec.RegenDelaySeconds;
        }

        private void Drain(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _sinceSpend = 0f;
            Current = Math.Max(0f, Current - amount);
            if (Current <= 0f)
            {
                IsDepleted = true;
            }
        }

        private static void RequireCost(float cost)
        {
            if (!(cost >= 0f) || float.IsInfinity(cost))
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "cost must be non-negative and finite");
            }
        }
    }
}
