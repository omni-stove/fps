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
        private readonly Func<T, float> _holsterSeconds;
        private float _holsterRemaining;
        private float _drawRemaining;
        private float _holsterTotal;
        private float _drawTotal;

        public event Action<int> Switched;

        public Loadout(IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster)
            : this(items, switchSeconds, onHolster, null)
        {
        }

        // drawSeconds は持ち替え先の武器を構える秒数を返す。null を返した武器は switchSeconds を使う。
        public Loadout(IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster, Func<T, float?> drawSeconds)
            : this(items, switchSeconds, onHolster, drawSeconds, null)
        {
        }

        // holsterSeconds は手放す武器をしまう秒数を返し、構える前にその分を待つ。
        public Loadout(
            IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster, Func<T, float?> drawSeconds, Func<T, float> holsterSeconds)
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
            _holsterSeconds = holsterSeconds;
        }

        // 並びに武器を足して作り直すとき、TrySelect で戻すと持ち替えの待ちが始まるので、構えていた番号のまま始める。
        public Loadout(
            IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster, Func<T, float?> drawSeconds, Func<T, float> holsterSeconds, int currentIndex)
            : this(items, switchSeconds, onHolster, drawSeconds, holsterSeconds)
        {
            if (currentIndex < 0 || currentIndex >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(currentIndex), currentIndex, "currentIndex is outside the loadout");
            }

            CurrentIndex = currentIndex;
        }

        public IReadOnlyList<T> Items { get; }

        public int Count => Items.Count;

        public float SwitchSeconds { get; }

        public int CurrentIndex { get; private set; }

        public T Current => Items[CurrentIndex];

        public bool IsSwitching => _holsterRemaining > 0f || _drawRemaining > 0f;

        public bool IsHolstering => _holsterRemaining > 0f;

        public float SwitchRemaining => _holsterRemaining + _drawRemaining;

        // 武器をどれだけ下げているか。0 が構えた状態、1 がしまい切った状態で、しまう間に 0 から 1 へ、構える間に 1 から 0 へ進む。
        public float SwitchLowering
        {
            get
            {
                if (_holsterRemaining > 0f)
                {
                    return 1f - (_holsterRemaining / _holsterTotal);
                }

                return _drawRemaining > 0f ? _drawRemaining / _drawTotal : 0f;
            }
        }

        // 今 index へ持ち替えたら構え終わるまでの秒数。
        public float SwitchSecondsTo(int index)
        {
            return HolsterSecondsNow() + DrawSecondsOf(index);
        }

        public float DrawSecondsOf(int index)
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

        public float HolsterSecondsOf(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index is outside the loadout");
            }

            if (_holsterSeconds == null)
            {
                return 0f;
            }

            var holster = _holsterSeconds(Items[index]);
            if (!(holster >= 0f) || float.IsInfinity(holster))
            {
                throw new InvalidOperationException("holster seconds of item " + index + " must be a non-negative finite number (got " + holster + ")");
            }

            return holster;
        }

        // 持ち替えの途中で選び直したときは、まだ構えていない武器をしまう時間は掛けず、前の武器をしまい終えていない分だけ残す。
        private float HolsterSecondsNow()
        {
            return IsSwitching ? _holsterRemaining : HolsterSecondsOf(CurrentIndex);
        }

        public bool TrySelect(int index)
        {
            if (index < 0 || index >= Items.Count || index == CurrentIndex)
            {
                return false;
            }

            var stillHolstering = IsHolstering;
            var holster = HolsterSecondsNow();
            var draw = DrawSecondsOf(index);
            _onHolster?.Invoke(Current);
            CurrentIndex = index;

            // しまう途中で選び直しても、下げかけた武器が上に戻らないよう、しまう秒数の全体は前のままにする。
            if (!stillHolstering)
            {
                _holsterTotal = holster;
            }

            _holsterRemaining = holster;
            _drawRemaining = draw;
            _drawTotal = draw;
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

            // 残りの合計を渡されたら、しまう分を引いた差の丸めで構える分が少しだけ残らないよう、そのまま終える。
            if (deltaSeconds >= SwitchRemaining)
            {
                _holsterRemaining = 0f;
                _drawRemaining = 0f;
                return;
            }

            var holstered = Math.Min(_holsterRemaining, deltaSeconds);
            _holsterRemaining -= holstered;
            _drawRemaining = Math.Max(0f, _drawRemaining - (deltaSeconds - holstered));
        }
    }
}
