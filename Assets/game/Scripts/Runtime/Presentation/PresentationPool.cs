using System;
using System.Collections.Generic;

namespace Fps.Runtime
{
    /// <summary>
    /// 演出の部品 (エフェクト・曳光弾・音源) を使い回すための、上限付きのプール。<see cref="WeaponPresentation"/> が持つ。
    /// <see cref="Rent"/> は使っていない部品があればそれを、無ければ上限までは新しく作り、上限に達していれば
    /// いちばん前に借りた部品を取り上げて渡す (演出は古いものから消えてよい)。
    /// 借りた部品は返す時刻を持ち、<see cref="ReleaseDue"/> が時刻の来たものを返す。時刻は呼ぶ側が渡す (テストで進められるよう)。
    /// </summary>
    /// <typeparam name="T">部品。同じインスタンスを 2 回入れない参照型。</typeparam>
    public sealed class PresentationPool<T>
        where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _activate;
        private readonly Action<T> _deactivate;
        private readonly List<T> _all = new List<T>();
        private readonly Stack<T> _idle = new Stack<T>();

        // 借りた順。先頭がいちばん古い。
        private readonly List<Lease> _active = new List<Lease>();

        /// <param name="capacity">作る部品の上限。1 以上。</param>
        /// <param name="create">部品を 1 つ作る。null を返してはいけない。</param>
        /// <param name="activate">貸す直前に呼ぶ (見えるようにするなど)。null 可。</param>
        /// <param name="deactivate">返したとき・取り上げたときに呼ぶ (隠す・止めるなど)。null 可。</param>
        public PresentationPool(int capacity, Func<T> create, Action<T> activate = null, Action<T> deactivate = null)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            Capacity = capacity;
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _activate = activate;
            _deactivate = deactivate;
        }

        /// <summary>作る部品の上限。</summary>
        public int Capacity { get; }

        /// <summary>これまでに作った部品の数。<see cref="Capacity"/> を超えない。</summary>
        public int Count => _all.Count;

        /// <summary>今貸している部品の数。</summary>
        public int ActiveCount => _active.Count;

        /// <summary>作った部品すべて (作った順)。</summary>
        public IReadOnlyList<T> Items => _all;

        /// <summary>
        /// 部品を 1 つ借りる。releaseAt は返す時刻 (<see cref="ReleaseDue"/> に渡す時刻と同じ物差し)。
        /// 上限に達していて空きが無ければ、いちばん前に借りた部品を返してから貸し直す。
        /// </summary>
        public T Rent(float releaseAt)
        {
            T item;
            if (_idle.Count > 0)
            {
                item = _idle.Pop();
            }
            else if (_all.Count < Capacity)
            {
                item = _create();
                if (item == null)
                {
                    throw new InvalidOperationException("the pool's create function returned null");
                }

                _all.Add(item);
            }
            else
            {
                item = _active[0].Item;
                _active.RemoveAt(0);
                _deactivate?.Invoke(item);
            }

            _active.Add(new Lease(item, releaseAt));
            _activate?.Invoke(item);
            return item;
        }

        /// <summary>貸している item の返す時刻を releaseAt に変える。貸していなければ false。</summary>
        public bool Extend(T item, float releaseAt)
        {
            var index = IndexOfActive(item);
            if (index < 0)
            {
                return false;
            }

            _active[index] = new Lease(item, releaseAt);
            return true;
        }

        /// <summary>貸している item を返す。貸していなければ何もせず false。</summary>
        public bool Release(T item)
        {
            var index = IndexOfActive(item);
            if (index < 0)
            {
                return false;
            }

            ReleaseAt(index);
            return true;
        }

        /// <summary>返す時刻が now 以前のものをすべて返し、返した数を返す。</summary>
        public int ReleaseDue(float now)
        {
            var released = 0;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].ReleaseAt <= now)
                {
                    ReleaseAt(i);
                    released++;
                }
            }

            return released;
        }

        /// <summary>今貸している部品を借りた順に into へ入れ直す (前の中身は消す)。返しながら回せるよう写しを作る。</summary>
        public void CopyActive(List<T> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var lease in _active)
            {
                into.Add(lease.Item);
            }
        }

        /// <summary>貸しているものをすべて返し、作った部品を destroy に渡して忘れる。</summary>
        public void Clear(Action<T> destroy)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                ReleaseAt(i);
            }

            if (destroy != null)
            {
                foreach (var item in _all)
                {
                    destroy(item);
                }
            }

            _all.Clear();
            _idle.Clear();
        }

        private int IndexOfActive(T item)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (ReferenceEquals(_active[i].Item, item))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ReleaseAt(int index)
        {
            var item = _active[index].Item;
            _active.RemoveAt(index);
            _deactivate?.Invoke(item);
            _idle.Push(item);
        }

        private readonly struct Lease
        {
            public T Item { get; }
            public float ReleaseAt { get; }

            public Lease(T item, float releaseAt)
            {
                Item = item;
                ReleaseAt = releaseAt;
            }
        }
    }
}
