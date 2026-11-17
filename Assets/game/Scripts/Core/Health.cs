using System;

namespace Shooter.Core
{
    public sealed class Health
    {
        public event Action Died;

        public event Action Changed;

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current == 0;

        public Health(int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be positive");
            }

            Max = max;
            Current = max;
        }

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

            var previous = Current;
            Current = Math.Max(0, Current - amount);
            if (Current != previous)
            {
                Changed?.Invoke();
            }

            if (IsDead)
            {
                Died?.Invoke();
            }
        }

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

            var previous = Current;
            Current = Math.Min(Max, Current + amount);
            if (Current != previous)
            {
                Changed?.Invoke();
            }
        }
    }
}
