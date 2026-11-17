using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class LoadoutSelection
    {
        private readonly IReadOnlyList<WeaponSpec>[] _options;
        private readonly int[] _selected;
        private readonly Func<string, bool> _isAvailable;

        public LoadoutSelection(
            LoadoutRule rule,
            WeaponCatalog catalog,
            IReadOnlyList<string> preferred,
            IReadOnlyList<string> fallback,
            Func<string, bool> isAvailable = null)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var options = ListOptions(rule, catalog);
            _options = new IReadOnlyList<WeaponSpec>[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                _options[i] = options[i];
            }

            _isAvailable = isAvailable;
            _selected = new int[_options.Length];
            var usePreferred = preferred != null && preferred.Count == _options.Length;
            var useFallback = fallback != null && fallback.Count == _options.Length;
            var restored = usePreferred;
            for (var slot = 0; slot < _options.Length; slot++)
            {
                var index = usePreferred ? IndexOf(slot, preferred[slot]) : -1;
                if (index < 0)
                {
                    restored = false;
                    index = useFallback ? IndexOf(slot, fallback[slot]) : -1;
                }

                if (index < 0)
                {
                    index = FirstUnused(slot);
                }

                _selected[slot] = index;
            }

            RestoredPreference = restored;
        }

        public LoadoutRule Rule { get; }

        public int SlotCount => _options.Length;

        public bool RestoredPreference { get; }

        // 既定のロードアウトにしかない武器を並べず catalog だけから並べるのは、サーバーが選択を ServerLoadout.Catalog から引くため。
        public static IReadOnlyList<IReadOnlyList<WeaponSpec>> ListOptions(LoadoutRule rule, WeaponCatalog catalog)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var slots = new List<IReadOnlyList<WeaponSpec>>(rule.SlotCount);
            for (var slot = 0; slot < rule.SlotCount; slot++)
            {
                var options = new List<WeaponSpec>();
                foreach (var weapon in catalog.All)
                {
                    if (rule.Accepts(slot, weapon.Category))
                    {
                        options.Add(weapon);
                    }
                }

                slots.Add(options.AsReadOnly());
            }

            return slots.AsReadOnly();
        }

        public IReadOnlyList<WeaponSpec> OptionsAt(int slot)
        {
            RequireSlot(slot);
            return _options[slot];
        }

        public int SelectedIndexAt(int slot)
        {
            RequireSlot(slot);
            return _selected[slot];
        }

        public WeaponSpec SelectedAt(int slot)
        {
            RequireSlot(slot);
            var index = _selected[slot];
            return index >= 0 ? _options[slot][index] : null;
        }

        public bool IsAvailableAt(int slot, int option)
        {
            RequireSlot(slot);
            if (option < 0 || option >= _options[slot].Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(option), option, "option must be in [0, " + _options[slot].Count + ")");
            }

            return IsAvailable(_options[slot][option]);
        }

        public bool Select(int slot, int option)
        {
            RequireSlot(slot);
            if (option < 0 || option >= _options[slot].Count || !IsAvailable(_options[slot][option]))
            {
                return false;
            }

            _selected[slot] = option;
            return true;
        }

        public bool Cycle(int slot, int step)
        {
            RequireSlot(slot);
            var available = 0;
            foreach (var option in _options[slot])
            {
                if (IsAvailable(option))
                {
                    available++;
                }
            }

            if (available == 0 || step == 0)
            {
                return false;
            }

            var current = Math.Max(0, _selected[slot]);
            var direction = step > 0 ? 1 : -1;
            // 1 歩ずつ進めると大きな step で回り続けるため、使える武器の数の周期で縮める。今の武器が使えないときは最初の 1 歩で周期に乗るので 1 歩残す
            var moves = Math.Abs((long)step);
            if (moves > available)
            {
                moves = (moves - 1) % available + 1;
            }

            var next = current;
            for (var moved = 0L; moved < moves; moved++)
            {
                next = NextAvailable(slot, next, direction);
            }

            _selected[slot] = next;
            return next != current;
        }

        public IReadOnlyList<string> WeaponIds
        {
            get
            {
                var ids = new string[_options.Length];
                for (var slot = 0; slot < ids.Length; slot++)
                {
                    ids[slot] = SelectedAt(slot)?.Id ?? string.Empty;
                }

                return ids;
            }
        }

        public string Validate()
        {
            var chosen = new WeaponSpec[_options.Length];
            for (var slot = 0; slot < chosen.Length; slot++)
            {
                chosen[slot] = SelectedAt(slot);
                if (chosen[slot] == null)
                {
                    return "slot " + slot + " has no weapon in the catalog that it accepts";
                }

                if (!IsAvailable(chosen[slot]))
                {
                    return "slot " + slot + " weapon " + chosen[slot].Id + " is not available";
                }
            }

            return Rule.Validate(chosen);
        }

        private int IndexOf(int slot, string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return -1;
            }

            var options = _options[slot];
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Id, weaponId, StringComparison.Ordinal))
                {
                    return IsAvailable(options[i]) ? i : -1;
                }
            }

            return -1;
        }

        private bool IsAvailable(WeaponSpec weapon)
        {
            return _isAvailable == null || _isAvailable(weapon.Id);
        }

        private int NextAvailable(int slot, int from, int direction)
        {
            var options = _options[slot];
            var count = options.Count;
            for (var offset = 1; offset <= count; offset++)
            {
                var candidate = ((from + direction * offset) % count + count) % count;
                if (IsAvailable(options[candidate]))
                {
                    return candidate;
                }
            }

            return from;
        }

        // 前のスロットで選んだ武器を避けて、そのままルールに合う並びになりやすくする
        private int FirstUnused(int slot)
        {
            var options = _options[slot];
            var firstAvailable = -1;
            for (var i = 0; i < options.Count; i++)
            {
                if (!IsAvailable(options[i]))
                {
                    continue;
                }

                if (firstAvailable < 0)
                {
                    firstAvailable = i;
                }

                var used = false;
                for (var earlier = 0; earlier < slot; earlier++)
                {
                    var other = _selected[earlier];
                    if (other >= 0 && string.Equals(_options[earlier][other].Id, options[i].Id, StringComparison.Ordinal))
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    return i;
                }
            }

            if (firstAvailable >= 0)
            {
                return firstAvailable;
            }

            // 使える武器が無いスロットを -1 にすると、選べる武器が無いスロットと見分けられず画面に何も出せない。先頭を見せ、送れないことは Validate が伝える
            return options.Count > 0 ? 0 : -1;
        }

        private void RequireSlot(int slot)
        {
            if (slot < 0 || slot >= _options.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must be in [0, " + _options.Length + ")");
            }
        }
    }
}
