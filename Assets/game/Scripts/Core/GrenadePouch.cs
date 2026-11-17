using System;

namespace Shooter.Core
{
    public sealed class GrenadePouch
    {
        private int _remaining;

        public GrenadePouch(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must not be negative");
            }

            Capacity = capacity;
            _remaining = capacity;
        }

        public event Action<int> Changed;

        public int Capacity { get; }

        public int Remaining => _remaining;

        public static GrenadePouch For(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            return new GrenadePouch(actions.Grenade == Grenade.On ? actions.GrenadeCount : 0);
        }

        public bool TryTake()
        {
            if (_remaining <= 0)
            {
                return false;
            }

            _remaining--;
            Changed?.Invoke(_remaining);
            return true;
        }

        public void Refill()
        {
            if (_remaining == Capacity)
            {
                return;
            }

            _remaining = Capacity;
            Changed?.Invoke(_remaining);
        }
    }
}
