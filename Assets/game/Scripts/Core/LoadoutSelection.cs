using System;
using System.Collections.Generic;

namespace Fps.Core
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

        /// <param name="rule">スロットの並びと各スロットが許す種別。</param>
        /// <param name="catalog">選べる武器の一覧。</param>
        /// <param name="preferred">
        /// 最初に選んでおくスロット順の Id (前に保存した並びなど)。null 可。本数がスロットの数と違えば使わない。
        /// そのスロットに並ばない Id のスロットは fallback で決める。
        /// </param>
        /// <param name="fallback">preferred で決まらなかったスロットに選んでおく、スロット順の Id (今の並び・既定の並び)。null 可。</param>
        /// <exception cref="ArgumentNullException">rule か catalog が null。</exception>
        public LoadoutSelection(LoadoutRule rule, WeaponCatalog catalog, IReadOnlyList<string> preferred, IReadOnlyList<string> fallback)
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

        /// <summary>slot 番のスロットで option 番目の武器を選ぶ。option が範囲外なら何もせず false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
        public bool Select(int slot, int option)
        {
            RequireSlot(slot);
            if (option < 0 || option >= _options[slot].Count)
            {
                return false;
            }

            _selected[slot] = option;
            return true;
        }

        /// <summary>
        /// slot 番のスロットの選択を step だけ前後に動かし、端では反対側へ回り込む。選べる武器が 1 つ以下なら何もせず false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">slot が範囲外。</exception>
        public bool Cycle(int slot, int step)
        {
            RequireSlot(slot);
            var count = _options[slot].Count;
            if (count <= 1 || step == 0)
            {
                return false;
            }

            var current = Math.Max(0, _selected[slot]);
            var next = ((current + step) % count + count) % count;
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
        /// 選べる武器が無いスロットがある・<see cref="Rule"/> に合わない (同じ武器を 2 回) なら送れない。
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
                    return i;
                }
            }

            return -1;
        }

        // 前のスロットで選んだ武器を避けて、そのままルールに合う並びになりやすくする
        private int FirstUnused(int slot)
        {
            var options = _options[slot];
            for (var i = 0; i < options.Count; i++)
            {
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
