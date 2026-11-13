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
    /// </remarks>
    public sealed class ServerLoadout
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
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

            Rule = rule;
            Weapons = copy.AsReadOnly();
            SwitchSeconds = switchSeconds;
            _burst = burst;
        }

        /// <summary>スロットの並びと各スロットが許す種別。</summary>
        public LoadoutRule Rule { get; }

        /// <summary>スロット順の武器。1 本以上で、Id は重ならない。</summary>
        public IReadOnlyList<WeaponSpec> Weapons { get; }

        /// <summary>持ち替えにかかる秒数。</summary>
        public float SwitchSeconds { get; }

        /// <summary>連射の制限を持っているプレイヤーの数 (一度でも撃って、まだ外していない人)。</summary>
        public int PlayerCount => _entries.Count;

        /// <summary>weaponId がこのロードアウトの武器なら true。大文字・小文字は区別する。</summary>
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

        /// <summary>playerId の連射の制限を捨てる (退出時)。持っていたら true。</summary>
        public bool Remove(int playerId)
        {
            return _entries.Remove(playerId);
        }

        /// <summary>全員の連射の制限を捨てる。次に撃ったときに満タンから作り直す (次の試合の開始時)。</summary>
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
