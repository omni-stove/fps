using System;

namespace Fps.Core
{
    /// <summary>
    /// 出撃ごとに持つグレネードの数。<see cref="For"/> で <see cref="ActionRules"/> から作り、投げるたびに <see cref="TryTake"/> で 1 つ減らし、
    /// リスポーンやチェックポイントで <see cref="Refill"/> して <see cref="Capacity"/> に戻す。
    /// </summary>
    public sealed class GrenadePouch
    {
        private int _remaining;

        /// <param name="capacity">持てる数 (戻したときの数)。0 以上。最初は満タン。</param>
        public GrenadePouch(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must not be negative");
            }

            Capacity = capacity;
            _remaining = capacity;
        }

        /// <summary>残りの数が変わったときに、新しい残りの数を渡して呼ばれる。</summary>
        public event Action<int> Changed;

        /// <summary>持てる数。</summary>
        public int Capacity { get; }

        /// <summary>残りの数。0 以上 <see cref="Capacity"/> 以下。</summary>
        public int Remaining => _remaining;

        /// <summary>
        /// actions で持つ数の袋。<see cref="Grenade.On"/> なら <see cref="ActionRules.GrenadeCount"/> 個、
        /// <see cref="Grenade.Off"/> なら 0 個 (数が設定されていても投げられない)。
        /// </summary>
        public static GrenadePouch For(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            return new GrenadePouch(actions.Grenade == Grenade.On ? actions.GrenadeCount : 0);
        }

        /// <summary>残りがあれば 1 つ減らして true。無ければ何もせず false。</summary>
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

        /// <summary>残りを <see cref="Capacity"/> に戻す。既に満タンなら <see cref="Changed"/> は呼ばない。</summary>
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
