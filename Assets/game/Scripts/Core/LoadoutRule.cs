using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ロードアウトのスロットの並びと、スロットごとに持てる <see cref="WeaponCategory"/> の集合。生成後は変わらない。
    /// スロットがそれぞれ何の役目か (メイン・サブなど) は持たず、どう並べるかはデータの側で決める。
    /// </summary>
    public sealed class LoadoutRule
    {
        private readonly HashSet<WeaponCategory>[] _accepted;

        /// <summary>スロット順の、そのスロットが許す種別 (重複なし・列挙値の昇順)。どのスロットも 1 つ以上。</summary>
        public IReadOnlyList<IReadOnlyList<WeaponCategory>> Slots { get; }

        /// <summary>スロットの数。1 以上。</summary>
        public int SlotCount => Slots.Count;

        /// <param name="slots">スロット順の、各スロットが許す種別。空・null 要素・種別が空のスロット・未定義の種別は不可。同じ種別の重複は 1 つにまとめる。</param>
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

        /// <summary>slot 番のスロットが category を許すなら true。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が 0 以上 <see cref="SlotCount"/> 未満でない。</exception>
        public bool Accepts(int slot, WeaponCategory category)
        {
            if (slot < 0 || slot >= _accepted.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must be in [0, " + _accepted.Length + ")");
            }

            return _accepted[slot].Contains(category);
        }

        /// <summary>
        /// weapons の i 本目を i 番スロットに入れるとして、このルールに合うか調べる。合えば null、合わなければ理由を返す。
        /// 本数が <see cref="SlotCount"/> と違う・null 要素・スロットが許さない種別・同じ Id の武器を 2 回入れる、のどれも不可。
        /// </summary>
        /// <exception cref="ArgumentNullException">weapons が null。</exception>
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
