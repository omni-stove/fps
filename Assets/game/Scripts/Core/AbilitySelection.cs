using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 「なし」を null や -1 にせず候補の先頭 (None) に置くのは、能力を持たない枠も武器と同じ Select / Cycle で選べるようにするため。
    public sealed class AbilitySelection
    {
        public const int DefaultSlotCount = 2;

        public const string None = "";

        public const int NoneIndex = 0;

        private readonly IReadOnlyList<string>[] _options;
        private readonly int[] _selected;
        private readonly Func<string, bool> _isAvailable;

        public AbilitySelection(
            IReadOnlyList<string> abilityIds,
            IReadOnlyList<string> preferred,
            IReadOnlyList<string> fallback,
            Func<string, bool> isAvailable = null,
            int slotCount = DefaultSlotCount)
            : this(ListOptions(abilityIds, slotCount), preferred, fallback, isAvailable)
        {
        }

        public AbilitySelection(
            IReadOnlyList<IReadOnlyList<string>> options,
            IReadOnlyList<string> preferred,
            IReadOnlyList<string> fallback,
            Func<string, bool> isAvailable = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = new IReadOnlyList<string>[options.Count];
            for (var slot = 0; slot < options.Count; slot++)
            {
                _options[slot] = CopyOptions(options[slot], slot);
            }

            _isAvailable = isAvailable;
            _selected = new int[_options.Length];
            var usePreferred = preferred != null && preferred.Count == _options.Length;
            var restored = usePreferred;
            var decided = new bool[_options.Length];

            // 前の枠の既定が後ろの枠の保存した値を押し出さないよう、先に全枠で保存した値を確定させてから既定で埋める
            for (var slot = 0; slot < _options.Length; slot++)
            {
                var index = usePreferred ? IndexOf(slot, preferred[slot]) : -1;
                if (index < 0)
                {
                    restored = false;
                    continue;
                }

                _selected[slot] = index;
                decided[slot] = true;
            }

            for (var slot = 0; slot < _options.Length; slot++)
            {
                if (decided[slot])
                {
                    continue;
                }

                // 既定のロードアウトの能力は枠の数より少ないことがあるので、足りない枠は「なし」にする
                var index = fallback != null && slot < fallback.Count ? IndexOf(slot, fallback[slot]) : -1;
                _selected[slot] = index < 0 ? NoneIndex : index;
            }

            RestoredPreference = restored;
        }

        public int SlotCount => _options.Length;

        public bool RestoredPreference { get; }

        public static IReadOnlyList<IReadOnlyList<string>> ListOptions(IReadOnlyList<string> abilityIds, int slotCount = DefaultSlotCount)
        {
            if (abilityIds == null)
            {
                throw new ArgumentNullException(nameof(abilityIds));
            }

            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "slotCount must not be negative");
            }

            var slots = new List<IReadOnlyList<string>>(slotCount);
            for (var slot = 0; slot < slotCount; slot++)
            {
                slots.Add(abilityIds);
            }

            return slots.AsReadOnly();
        }

        public IReadOnlyList<string> OptionsAt(int slot)
        {
            RequireSlot(slot);
            return _options[slot];
        }

        public int SelectedIndexAt(int slot)
        {
            RequireSlot(slot);
            return _selected[slot];
        }

        public string SelectedAt(int slot)
        {
            RequireSlot(slot);
            return _options[slot][_selected[slot]];
        }

        public bool IsAvailableAt(int slot, int option)
        {
            RequireSlot(slot);
            RequireOption(slot, option);
            return IsAvailable(slot, option);
        }

        public bool Select(int slot, int option)
        {
            RequireSlot(slot);
            if (option < 0 || option >= _options[slot].Count || !IsAvailable(slot, option))
            {
                return false;
            }

            _selected[slot] = option;
            return true;
        }

        public bool Cycle(int slot, int step)
        {
            RequireSlot(slot);
            if (step == 0)
            {
                return false;
            }

            var available = 0;
            for (var option = 0; option < _options[slot].Count; option++)
            {
                if (IsAvailable(slot, option))
                {
                    available++;
                }
            }

            var current = _selected[slot];
            var direction = step > 0 ? 1 : -1;

            // 1 歩ずつ進めると大きな step で回り続けるため、選べる候補の数の周期で縮める。今の候補が選べないときは最初の 1 歩で周期に乗るので 1 歩残す
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

        public IReadOnlyList<string> AbilityIds
        {
            get
            {
                var ids = new string[_options.Length];
                for (var slot = 0; slot < ids.Length; slot++)
                {
                    ids[slot] = SelectedAt(slot);
                }

                return ids;
            }
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<string> CopyOptions(IReadOnlyList<string> options, int slot)
        {
            var ids = options;
            if (ids == null)
            {
                throw new ArgumentException("slot " + slot + " has no list of abilities", nameof(options));
            }

            var copy = new List<string>(ids.Count + 1) { None };
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    throw new ArgumentException("slot " + slot + " option " + i + " has no ability id", nameof(options));
                }

                if (copy.Contains(ids[i]))
                {
                    throw new ArgumentException("slot " + slot + " lists ability " + ids[i] + " more than once", nameof(options));
                }

                copy.Add(ids[i]);
            }

            return copy.AsReadOnly();
        }

        private int IndexOf(int slot, string abilityId)
        {
            if (abilityId == null)
            {
                return -1;
            }

            var options = _options[slot];
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], abilityId, StringComparison.Ordinal))
                {
                    return IsAvailable(slot, i) ? i : -1;
                }
            }

            return -1;
        }

        // 同じ能力を 2 つの枠に持たせても使える回数は増えないので、ほかの枠で選んだ能力は選べないものとして扱う
        private bool IsAvailable(int slot, int option)
        {
            if (option == NoneIndex)
            {
                return true;
            }

            var id = _options[slot][option];
            if (_isAvailable != null && !_isAvailable(id))
            {
                return false;
            }

            for (var other = 0; other < _options.Length; other++)
            {
                if (other != slot && string.Equals(_options[other][_selected[other]], id, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private int NextAvailable(int slot, int from, int direction)
        {
            var count = _options[slot].Count;
            for (var offset = 1; offset <= count; offset++)
            {
                var candidate = ((from + direction * offset) % count + count) % count;
                if (IsAvailable(slot, candidate))
                {
                    return candidate;
                }
            }

            return from;
        }

        private void RequireOption(int slot, int option)
        {
            if (option < 0 || option >= _options[slot].Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(option), option, "option must be in [0, " + _options[slot].Count + ")");
            }
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
