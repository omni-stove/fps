using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// ロードアウトを選ぶ画面の中身。<see cref="LoadoutRule"/> のスロットごとに、<see cref="WeaponCatalog"/> の武器のうち
    /// そのスロットが許す種別のものを一覧の順に並べ (<see cref="ListOptions"/>)、スロットごとに 1 つを選んでいる状態を持つ。
    /// 選んだ並びがルールに合うか (同じ武器を 2 つのスロットに入れていないか) は <see cref="Validate"/> で確かめる。
    /// </summary>
    /// <remarks>
    /// 並べる武器はサーバーが選択を引く一覧 (<see cref="ServerLoadout.Catalog"/>) と同じものなので、既定のロードアウトにしかない武器は並べない。
    /// </remarks>
    public sealed class LoadoutSelection
    {
        private readonly IReadOnlyList<WeaponSpec>[] _options;
        private readonly int[] _selected;
        private readonly Func<string, bool> _isAvailable;

        /// <param name="rule">スロットの並びと各スロットが許す種別。</param>
        /// <param name="catalog">選べる武器の一覧。</param>
        /// <param name="preferred">
        /// 最初に選んでおくスロット順の Id (前に保存した並びなど)。null 可。本数がスロットの数と違えば使わない。
        /// そのスロットに並ばない Id のスロットは fallback で決める。
        /// </param>
        /// <param name="fallback">preferred で決まらなかったスロットに選んでおく、スロット順の Id (今の並び・既定の並び)。null 可。</param>
        /// <param name="isAvailable">
        /// 武器の Id を受け取り、その武器がもう使えるかを返す。null なら全部使える。
        /// 使えない武器は一覧に並んだまま選ばれず、送れない。preferred・fallback の使えない Id は並ばない Id と同じに扱う。
        /// </param>
        /// <exception cref="ArgumentNullException">rule か catalog が null。</exception>
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

        /// <summary>スロットの並びと各スロットが許す種別。</summary>
        public LoadoutRule Rule { get; }

        /// <summary>スロットの数。</summary>
        public int SlotCount => _options.Length;

        /// <summary>コンストラクタの preferred が全スロットでそのまま選べたら true (保存した並びを復元できた)。</summary>
        public bool RestoredPreference { get; }

        /// <summary>
        /// rule のスロットごとに、catalog の武器のうちそのスロットが許す種別 (<see cref="LoadoutRule.Accepts"/>) のものを
        /// 一覧の順に並べる。どの武器も入らないスロットは空の列になる。
        /// </summary>
        /// <exception cref="ArgumentNullException">rule か catalog が null。</exception>
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

        /// <summary>slot 番のスロットに選べる武器 (一覧の順)。空のこともある。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
        public IReadOnlyList<WeaponSpec> OptionsAt(int slot)
        {
            RequireSlot(slot);
            return _options[slot];
        }

        /// <summary>slot 番のスロットで選んでいる武器の、<see cref="OptionsAt"/> の中の位置。選べる武器が無ければ -1。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
        public int SelectedIndexAt(int slot)
        {
            RequireSlot(slot);
            return _selected[slot];
        }

        /// <summary>slot 番のスロットで選んでいる武器。選べる武器が無ければ null。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
        public WeaponSpec SelectedAt(int slot)
        {
            RequireSlot(slot);
            var index = _selected[slot];
            return index >= 0 ? _options[slot][index] : null;
        }

        /// <summary>slot 番のスロットの option 番目の武器が使えるか。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot か option が範囲外。</exception>
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

        /// <summary>slot 番のスロットで option 番目の武器を選ぶ。option が範囲外か、その武器が使えなければ何もせず false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
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

        /// <summary>
        /// slot 番のスロットの選択を、使える武器だけを数えて step だけ前後に動かし、端では反対側へ回り込む。
        /// 移れる使える武器が無ければ何もせず false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
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

        /// <summary>選んでいる武器の Id (スロット順)。選べる武器が無いスロットは空文字。</summary>
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

        /// <summary>
        /// 選んでいる並びを送れるか。送れるなら null、送れなければ理由。
        /// 選べる武器が無いスロットがある・使えない武器を選んでいる・<see cref="Rule"/> に合わない (同じ武器を 2 回) なら送れない。
        /// </summary>
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
