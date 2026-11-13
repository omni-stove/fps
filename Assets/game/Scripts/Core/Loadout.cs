using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 持っている武器の並びと、今どれを持っているか・持ち替え中かどうか。時間は <see cref="Tick"/> で外から進める。
    /// 持ち替え中は撃てない扱いにするので、呼ぶ側は撃つ前に <see cref="IsSwitching"/> を見る。
    /// </summary>
    /// <typeparam name="T">武器の型。オフラインは <see cref="Weapon"/>、サーバーは <see cref="ServerWeapon"/>。</typeparam>
    public sealed class Loadout<T>
        where T : class
    {
        private readonly Action<T> _onHolster;
        private float _switchRemaining;

        /// <summary>番号が変わったときに呼ばれる。引数は新しい番号。</summary>
        public event Action<int> Switched;

        /// <param name="items">持つ武器。1 本以上で、null 要素は不可。</param>
        /// <param name="switchSeconds">持ち替えにかかる秒数。0 以上の有限値。</param>
        /// <param name="onHolster">武器をしまうときに呼ぶ処理 (リロードの中断など)。null なら何もしない。</param>
        public Loadout(IReadOnlyList<T> items, float switchSeconds, Action<T> onHolster)
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
        }

        /// <summary>持っている武器。生成後は変わらない。</summary>
        public IReadOnlyList<T> Items { get; }

        /// <summary>武器の数。1 以上。</summary>
        public int Count => Items.Count;

        /// <summary>持ち替えにかかる秒数。</summary>
        public float SwitchSeconds { get; }

        /// <summary>今持っている武器の番号。最初は 0。</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>今持っている武器。持ち替え中は持ち替え先の武器。</summary>
        public T Current => Items[CurrentIndex];

        /// <summary>持ち替えの途中なら true。この間は撃てない。</summary>
        public bool IsSwitching => _switchRemaining > 0f;

        /// <summary>
        /// index の武器に持ち替える。範囲外か今の番号と同じなら何もせず false。
        /// 持ち替え中でも受け付け、持ち替えの時間はそこから数え直す。
        /// </summary>
        public bool TrySelect(int index)
        {
            if (index < 0 || index >= Items.Count || index == CurrentIndex)
            {
                return false;
            }

            _onHolster?.Invoke(Current);
            CurrentIndex = index;
            _switchRemaining = SwitchSeconds;
            Switched?.Invoke(index);
            return true;
        }

        /// <summary>次の番号の武器に持ち替える。最後の次は 0 番。1 本しかなければ false。</summary>
        public bool SelectNext()
        {
            return TrySelect((CurrentIndex + 1) % Items.Count);
        }

        /// <summary>前の番号の武器に持ち替える。0 番の前は最後。1 本しかなければ false。</summary>
        public bool SelectPrevious()
        {
            return TrySelect((CurrentIndex + Items.Count - 1) % Items.Count);
        }

        /// <summary>持ち替えの残り時間を deltaSeconds 進める。0 以上の有限値。</summary>
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
