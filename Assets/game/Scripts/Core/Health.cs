using System;

namespace Fps.Core
{
    /// <summary>
    /// 体力。ダメージと回復を受け、0 になったら死亡する。死亡後はダメージも回復も受けない。
    /// </summary>
    public sealed class Health
    {
        /// <summary>体力が 0 になった瞬間に 1 度だけ呼ばれる。</summary>
        public event Action Died;

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current == 0;

        /// <param name="max">最大体力。1 以上。開始時は満タン。</param>
        public Health(int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be positive");
            }

            Max = max;
            Current = max;
        }

        /// <summary>体力を amount 減らす。0 未満にはならない。</summary>
        public void TakeDamage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (IsDead)
            {
                return;
            }

            Current = Math.Max(0, Current - amount);
            if (IsDead)
            {
                Died?.Invoke();
            }
        }

        /// <summary>体力を amount 増やす。Max を超えない。</summary>
        public void Heal(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (IsDead)
            {
                return;
            }

            Current = Math.Min(Max, Current + amount);
        }
    }
}
