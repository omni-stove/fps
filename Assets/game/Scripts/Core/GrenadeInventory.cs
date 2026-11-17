using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 出撃ごとに持つ投げ物。<see cref="GrenadeCatalog.Carried"/> の種類ごとに <see cref="GrenadePouch"/> を 1 つ持ち、
    /// どれも <see cref="GrenadePouch.For"/> の数にする。投げる種類は <see cref="Selected"/> で、<see cref="SelectNext"/> で順に切り替える。
    /// </summary>
    public sealed class GrenadeInventory
    {
        private readonly List<GrenadeSpec> _types = new List<GrenadeSpec>();
        private readonly List<GrenadePouch> _pouches = new List<GrenadePouch>();
        private int _selected;

        public GrenadeInventory(GrenadeCatalog catalog, ActionRules actions)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            foreach (var type in catalog.Carried)
            {
                var pouch = GrenadePouch.For(actions);
                pouch.Changed += OnPouchChanged;
                _types.Add(type);
                _pouches.Add(pouch);
            }
        }

        private GrenadeInventory()
        {
        }

        /// <summary>残りの数か、選んだ種類が変わったときに呼ばれる。</summary>
        public event Action Changed;

        public static GrenadeInventory Empty => new GrenadeInventory();

        public int Count => _types.Count;

        public int SelectedIndex => _types.Count == 0 ? -1 : _selected;

        /// <summary>何も持たなければ null。</summary>
        public GrenadeSpec Selected => _types.Count == 0 ? null : _types[_selected];

        public int SelectedRemaining => _types.Count == 0 ? 0 : _pouches[_selected].Remaining;

        public int SelectedCapacity => _types.Count == 0 ? 0 : _pouches[_selected].Capacity;

        public int TotalRemaining
        {
            get
            {
                var total = 0;
                foreach (var pouch in _pouches)
                {
                    total += pouch.Remaining;
                }

                return total;
            }
        }

        public GrenadeSpec TypeAt(int index) => _types[Check(index)];

        public int RemainingAt(int index) => _pouches[Check(index)].Remaining;

        public int CapacityAt(int index) => _pouches[Check(index)].Capacity;

        public int IndexOf(string typeId)
        {
            for (var i = 0; i < _types.Count; i++)
            {
                if (string.Equals(_types[i].Id, typeId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        // 残りが 0 の種類も飛ばさないのは、表示される順番を残りの数で変えないため
        public bool SelectNext()
        {
            if (_types.Count < 2)
            {
                return false;
            }

            _selected = (_selected + 1) % _types.Count;
            Changed?.Invoke();
            return true;
        }

        public bool Select(string typeId)
        {
            var at = IndexOf(typeId);
            if (at < 0)
            {
                return false;
            }

            if (at != _selected)
            {
                _selected = at;
                Changed?.Invoke();
            }

            return true;
        }

        public bool TryTake(int index)
        {
            return _pouches[Check(index)].TryTake();
        }

        public void Refill()
        {
            foreach (var pouch in _pouches)
            {
                pouch.Refill();
            }
        }

        private int Check(int index)
        {
            if ((uint)index >= (uint)_types.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + _types.Count);
            }

            return index;
        }

        private void OnPouchChanged(int remaining)
        {
            Changed?.Invoke();
        }
    }
}
