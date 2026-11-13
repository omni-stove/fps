using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// サーバーが試合で全員に持たせるロードアウト (<see cref="LoadoutRule"/> に合うスロット順の <see cref="WeaponSpec"/> と持ち替え秒数) と、
    /// 各プレイヤーの今の武器の <see cref="WeaponSpec.FireInterval"/> で作った連射の制限 (<see cref="ShotBudget"/>)。
    /// クライアントは撃った武器の Id だけを送り、ダメージ・射程・減衰・連射間隔はすべてサーバーが持たせている武器の値を使う。
    /// </summary>
    /// <remarks>
    /// 今どの武器を持っているかはここでは持たず、射撃のたびに呼ぶ側 (持ち替えを判定するもの) から受け取る。
    /// 前の射撃と違う武器で撃ったら (持ち替えた・出し直した)、その武器の間隔で <see cref="ShotBudget"/> を作り直す。
    /// <para>
    /// 武器の一覧 (<see cref="Catalog"/>) を渡すと、各プレイヤーが一覧の武器から選んだ並びを <see cref="TryChoose"/> で
    /// <see cref="Rule"/> に照らして検査し、通ればそのプレイヤーの次の出現から配る武器 (<see cref="WeaponsFor"/>) として控える。
    /// 選んだ並びは <see cref="ResetBudgets"/> (次の試合) では消えず、<see cref="Remove"/> (退出) で消える。
    /// </para>
    /// </remarks>
    public sealed class ServerLoadout
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<int, IReadOnlyList<WeaponSpec>> _choices = new Dictionary<int, IReadOnlyList<WeaponSpec>>();
        private readonly HashSet<string> _weaponIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly int _burst;

        /// <param name="rule">スロットの並びと各スロットが許す種別。</param>
        /// <param name="weapons">スロット順の武器。rule に合うこと (<see cref="LoadoutRule.Validate"/>)。渡した列は複製して持つ。</param>
        /// <param name="switchSeconds">持ち替えにかかる秒数。0 以上の有限値。</param>
        /// <param name="burst">連射の制限で溜められる発数 (<see cref="ShotBudget.Burst"/>)。1 以上。</param>
        /// <exception cref="ArgumentNullException">rule か weapons が null。</exception>
        /// <exception cref="ArgumentException">weapons が rule に合わない。</exception>
        /// <exception cref="ArgumentOutOfRangeException">switchSeconds か burst が範囲外。</exception>
        public ServerLoadout(LoadoutRule rule, IReadOnlyList<WeaponSpec> weapons, float switchSeconds, int burst = ShotBudget.DefaultBurst)
            : this(rule, weapons, null, switchSeconds, burst)
        {
        }

        /// <param name="rule">スロットの並びと各スロットが許す種別。プレイヤーが選んだ並びもこれで検査する。</param>
        /// <param name="weapons">スロット順の既定の武器。rule に合うこと (<see cref="LoadoutRule.Validate"/>)。渡した列は複製して持つ。</param>
        /// <param name="catalog">プレイヤーが選べる武器の一覧。null なら選べない (全員に weapons を配る)。</param>
        /// <param name="switchSeconds">持ち替えにかかる秒数。0 以上の有限値。</param>
        /// <param name="burst">連射の制限で溜められる発数 (<see cref="ShotBudget.Burst"/>)。1 以上。</param>
        /// <exception cref="ArgumentNullException">rule か weapons が null。</exception>
        /// <exception cref="ArgumentException">weapons が rule に合わない。</exception>
        /// <exception cref="ArgumentOutOfRangeException">switchSeconds か burst が範囲外。</exception>
        public ServerLoadout(
            LoadoutRule rule, IReadOnlyList<WeaponSpec> weapons, WeaponCatalog catalog, float switchSeconds, int burst = ShotBudget.DefaultBurst)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (!(switchSeconds >= 0f) || float.IsInfinity(switchSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(switchSeconds), switchSeconds, "switchSeconds must be a non-negative finite number");
            }

            if (burst <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(burst), burst, "burst must be positive");
            }

            var copy = new List<WeaponSpec>(weapons);
            var reason = rule.Validate(copy);
            if (reason != null)
            {
                throw new ArgumentException("weapons do not fit the loadout rule: " + reason, nameof(weapons));
            }

            foreach (var weapon in copy)
            {
                _weaponIds.Add(weapon.Id);
            }

            if (catalog != null)
            {
                foreach (var weapon in catalog.All)
                {
                    _weaponIds.Add(weapon.Id);
                }
            }

            Catalog = catalog;
            Rule = rule;
            Weapons = copy.AsReadOnly();
            SwitchSeconds = switchSeconds;
            _burst = burst;
        }

        /// <summary>スロットの並びと各スロットが許す種別。</summary>
        public LoadoutRule Rule { get; }

        /// <summary>スロット順の既定の武器。選んでいないプレイヤーに配る。1 本以上で、Id は重ならない。</summary>
        public IReadOnlyList<WeaponSpec> Weapons { get; }

        /// <summary>プレイヤーが選べる武器の一覧。選べないなら null。</summary>
        public WeaponCatalog Catalog { get; }

        /// <summary>選んだ並びを控えているプレイヤーの数。</summary>
        public int ChoiceCount => _choices.Count;

        /// <summary>持ち替えにかかる秒数。</summary>
        public float SwitchSeconds { get; }

        /// <summary>連射の制限を持っているプレイヤーの数 (一度でも撃って、まだ外していない人)。</summary>
        public int PlayerCount => _entries.Count;

        /// <summary>weaponId が既定の武器か <see cref="Catalog"/> の武器なら true。大文字・小文字は区別する。</summary>
        public bool Contains(string weaponId)
        {
            return weaponId != null && _weaponIds.Contains(weaponId);
        }

        /// <summary>
        /// held を持っている人が weaponId で撃ってよいか、武器だけを調べる (連射の制限は減らさない)。
        /// weaponId がロードアウトに無ければ <see cref="ShotRejection.UnknownWeapon"/>、held が null (装備前) なら
        /// <see cref="ShotRejection.NoWeapon"/>、held と違えば <see cref="ShotRejection.WeaponMismatch"/>。
        /// </summary>
        public ShotRejection CheckWeapon(WeaponSpec held, string weaponId)
        {
            if (!Contains(weaponId))
            {
                return ShotRejection.UnknownWeapon;
            }

            if (held == null)
            {
                return ShotRejection.NoWeapon;
            }

            if (!string.Equals(held.Id, weaponId, StringComparison.Ordinal))
            {
                return ShotRejection.WeaponMismatch;
            }

            return ShotRejection.None;
        }

        /// <summary>
        /// <see cref="CheckWeapon"/> に通ったら、playerId の連射の制限から nowSeconds 時点で 1 発ぶん減らす。
        /// 前の射撃と held が違えば、先に held の <see cref="WeaponSpec.FireInterval"/> で制限を満タンから作り直す。
        /// 間隔より速ければ <see cref="ShotRejection.FireRateExceeded"/>。武器が違う射撃では制限を減らさない。
        /// </summary>
        /// <param name="playerId">撃ったプレイヤーの Id。</param>
        /// <param name="held">サーバーがそのプレイヤーに持たせている今の武器。装備前は null。</param>
        /// <param name="weaponId">クライアントが申告した武器 Id。</param>
        /// <param name="nowSeconds">サーバーの今の時刻 (秒)。有限値。</param>
        public ShotRejection TryFire(int playerId, WeaponSpec held, string weaponId, double nowSeconds)
        {
            var rejection = CheckWeapon(held, weaponId);
            if (rejection != ShotRejection.None)
            {
                return rejection;
            }

            if (!_entries.TryGetValue(playerId, out var entry) || !string.Equals(entry.WeaponId, held.Id, StringComparison.Ordinal))
            {
                entry = new Entry(held.Id, new ShotBudget(held.FireInterval, _burst));
                _entries[playerId] = entry;
            }

            return entry.Budget.TryConsume(nowSeconds) ? ShotRejection.None : ShotRejection.FireRateExceeded;
        }

        /// <summary>playerId の今の連射の制限。まだ撃っていない・外したなら false と null。</summary>
        public bool TryGetBudget(int playerId, out ShotBudget budget)
        {
            if (_entries.TryGetValue(playerId, out var entry))
            {
                budget = entry.Budget;
                return true;
            }

            budget = null;
            return false;
        }

        /// <summary>
        /// weaponIds (スロット順の武器の Id) を <see cref="Catalog"/> から引き、<see cref="Rule"/> に合うか調べる。
        /// 合えば null と引いた武器を、合わなければ理由と null を返す。理由には一覧に無い Id そのものは入れない (送ってきた側の文字列をログに出さないため)。
        /// 一覧が無い・weaponIds が null・本数がスロットの数と違う・空の Id・一覧に無い Id・スロットが許さない種別・同じ武器を 2 回、のどれも不可。
        /// </summary>
        public string Resolve(IReadOnlyList<string> weaponIds, out IReadOnlyList<WeaponSpec> weapons)
        {
            weapons = null;
            if (Catalog == null)
            {
                return "this match has no weapon catalog to choose from";
            }

            if (weaponIds == null)
            {
                return "no weapons were sent";
            }

            if (weaponIds.Count != Rule.SlotCount)
            {
                return "expected " + Rule.SlotCount + " weapon(s) (one per slot) but got " + weaponIds.Count;
            }

            var specs = new List<WeaponSpec>(weaponIds.Count);
            for (var i = 0; i < weaponIds.Count; i++)
            {
                var id = weaponIds[i];
                if (string.IsNullOrEmpty(id))
                {
                    return "slot " + i + " has no weapon id";
                }

                if (!Catalog.TryGet(id, out var spec))
                {
                    return "slot " + i + " has a weapon id that is not in the catalog (length " + id.Length + ")";
                }

                specs.Add(spec);
            }

            var reason = Rule.Validate(specs);
            if (reason != null)
            {
                return reason;
            }

            weapons = specs.AsReadOnly();
            return null;
        }

        /// <summary>
        /// playerId が選んだ weaponIds を <see cref="Resolve"/> で検査し、通ればそのプレイヤーの次の出現から配る武器として控えて true。
        /// 通らなければ理由を reason に入れて false (前に控えた並びはそのまま)。
        /// </summary>
        public bool TryChoose(int playerId, IReadOnlyList<string> weaponIds, out string reason)
        {
            reason = Resolve(weaponIds, out var weapons);
            if (reason != null)
            {
                return false;
            }

            _choices[playerId] = weapons;
            return true;
        }

        /// <summary>playerId が選んだ並びを控えていれば true。</summary>
        public bool HasChoice(int playerId)
        {
            return _choices.ContainsKey(playerId);
        }

        /// <summary>playerId に配る武器 (スロット順)。<see cref="TryChoose"/> で選んでいればその並び、なければ <see cref="Weapons"/>。</summary>
        public IReadOnlyList<WeaponSpec> WeaponsFor(int playerId)
        {
            return _choices.TryGetValue(playerId, out var chosen) ? chosen : Weapons;
        }

        /// <summary>playerId の連射の制限と選んだ並びを捨てる (退出時)。どちらかを持っていたら true。</summary>
        public bool Remove(int playerId)
        {
            var hadBudget = _entries.Remove(playerId);
            var hadChoice = _choices.Remove(playerId);
            return hadBudget || hadChoice;
        }

        /// <summary>全員の連射の制限を捨てる。次に撃ったときに満タンから作り直す (次の試合の開始時)。選んだ並びは残す。</summary>
        public void ResetBudgets()
        {
            _entries.Clear();
        }

        private readonly struct Entry
        {
            public Entry(string weaponId, ShotBudget budget)
            {
                WeaponId = weaponId;
                Budget = budget;
            }

            public string WeaponId { get; }

            public ShotBudget Budget { get; }
        }
    }
}
