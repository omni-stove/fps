using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class LoadoutRule
    {
        private readonly HashSet<WeaponCategory>[] _accepted;

        public IReadOnlyList<IReadOnlyList<WeaponCategory>> Slots { get; }

        public int SlotCount => Slots.Count;

        public LoadoutRule(IEnumerable<IEnumerable<WeaponCategory>> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var accepted = new List<HashSet<WeaponCategory>>();
            var ordered = new List<IReadOnlyList<WeaponCategory>>();
            foreach (var slot in slots)
            {
                var index = accepted.Count;
                if (slot == null)
                {
                    throw new ArgumentException("slot " + index + " is null", nameof(slots));
                }

                var set = new HashSet<WeaponCategory>();
                foreach (var category in slot)
                {
                    if (!Enum.IsDefined(typeof(WeaponCategory), category))
                    {
                        throw new ArgumentException(
                            "slot " + index + " has an undefined category (" + (int)category + ")", nameof(slots));
                    }

                    set.Add(category);
                }

                if (set.Count == 0)
                {
                    throw new ArgumentException("slot " + index + " accepts no category", nameof(slots));
                }

                var sorted = new List<WeaponCategory>(set);
                sorted.Sort();
                accepted.Add(set);
                ordered.Add(sorted.AsReadOnly());
            }

            if (accepted.Count == 0)
            {
                throw new ArgumentException("slots must not be empty", nameof(slots));
            }

            _accepted = accepted.ToArray();
            Slots = ordered.AsReadOnly();
        }

        public bool Accepts(int slot, WeaponCategory category)
        {
            if (slot < 0 || slot >= _accepted.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must be in [0, " + _accepted.Length + ")");
            }

            return _accepted[slot].Contains(category);
        }

        public string Validate(IReadOnlyList<WeaponSpec> weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (weapons.Count != _accepted.Length)
            {
                return "expected " + _accepted.Length + " weapon(s) (one per slot) but got " + weapons.Count;
            }

            var slotById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    return "slot " + i + " has no weapon";
                }

                if (!_accepted[i].Contains(weapon.Category))
                {
                    return "slot " + i + " does not accept " + weapon.Category + " (weapon '" + weapon.LogName + "')";
                }

                if (slotById.TryGetValue(weapon.Id, out var other))
                {
                    return "slot " + i + " has weapon '" + weapon.LogName + "' (id '" + weapon.Id + "') already in slot " + other;
                }

                slotById.Add(weapon.Id, i);
            }

            return null;
        }
    }
}
