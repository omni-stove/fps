using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 武器の型を型引数にするのは、オフラインの Weapon とサーバーの ServerWeapon で同じ持ち替えを使うため。
    public sealed class Loadout<T>
        where T : class
    {
        private readonly Action<T> _onHolster;
        private readonly Func<T, float?> _drawSeconds;
        private float _switchRemaining;

        public event Action<int> Switched;

        public Loadout(IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster)
            : this(items, switchSeconds, onHolster, null)
        {
        }

        // drawSeconds は持ち替え先の武器を構える秒数を返す。null を返した武器は switchSeconds を使う。
        public Loadout(IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster, Func<T, float?> drawSeconds)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("items must not be empty", nameof(items));
            }

            var list = new List<T>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    throw new ArgumentException("items must not contain null (index " + i + ")", nameof(items));
                }

                list.Add(items[i]);
            }

            if (!(switchSeconds >= 0f) || float.IsInfinity(switchSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(switchSeconds), switchSeconds, "switchSeconds must be a non-negative finite number");
            }

            // 渡されたリストを後から書き換えられても番号がずれないよう写しを持つ
            Items = list.AsReadOnly();
            SwitchSeconds = switchSeconds;
            _onHolster = onHolster;
            _drawSeconds = drawSeconds;
        }

        public IReadOnlyList<T> Items { get; }

        public int Count => Items.Count;

        public float SwitchSeconds { get; }

        public int CurrentIndex { get; private set; }

        public T Current => Items[CurrentIndex];

        public bool IsSwitching => _switchRemaining > 0f;

        public float SwitchSecondsTo(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index is outside the loadout");
            }

            var draw = _drawSeconds?.Invoke(Items[index]);
            if (!draw.HasValue)
            {
                return SwitchSeconds;
            }

            if (!(draw.Value >= 0f) || float.IsInfinity(draw.Value))
            {
                throw new InvalidOperationException("draw seconds of item " + index + " must be a non-negative finite number (got " + draw.Value + ")");
            }

            return draw.Value;
        }

        public bool TrySelect(int index)
        {
            if (index < 0 || index >= Items.Count || index == CurrentIndex)
            {
                return false;
            }

            var seconds = SwitchSecondsTo(index);
            _onHolster?.Invoke(Current);
            CurrentIndex = index;
            _switchRemaining = seconds;
            Switched?.Invoke(index);
            return true;
        }

        public bool SelectNext()
        {
            return TrySelect((CurrentIndex + 1) % Items.Count);
        }

        public bool SelectPrevious()
        {
            return TrySelect((CurrentIndex + Items.Count - 1) % Items.Count);
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _switchRemaining = Math.Max(0f, _switchRemaining - deltaSeconds);
        }
    }
}
