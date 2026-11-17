using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    // 上限に達したらいちばん前に借りた部品を取り上げて渡すのは、演出は古いものから消えてよいため。
    // 時刻を Time から読まず呼ぶ側が渡すのは、テストで進められるようにするため。
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

        public int Capacity { get; }

        public int Count => _all.Count;

        public int ActiveCount => _active.Count;

        public IReadOnlyList<T> Items => _all;

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

        // 呼ぶ側が返しながら回せるよう、貸している並びをそのまま渡さず写しを作る。
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
