using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// サーバーが各プレイヤーに持たせている武器 (<see cref="WeaponCatalog"/> から引いた <see cref="WeaponSpec"/>) と、
    /// その武器の <see cref="WeaponSpec.FireInterval"/> で作った連射の制限 (<see cref="ShotBudget"/>)。
    /// クライアントは撃った武器の Id だけを送り、ダメージ・射程・減衰・連射間隔はすべてここで引いた値を使う。
    /// </summary>
    public sealed class ServerLoadout
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly int _burst;

        /// <param name="catalog">引ける武器の一覧。</param>
        /// <param name="burst">連射の制限で溜められる発数 (<see cref="ShotBudget.Burst"/>)。1 以上。</param>
        public ServerLoadout(WeaponCatalog catalog, int burst = ShotBudget.DefaultBurst)
        {
            if (burst <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(burst), burst, "burst must be positive");
            }

            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _burst = burst;
        }

        /// <summary>引ける武器の一覧。</summary>
        public WeaponCatalog Catalog { get; }

        /// <summary>武器を持たせているプレイヤーの数。</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// playerId に weaponId の武器を持たせ、その武器を返す。前の武器と連射の制限は捨てる (同じ武器でも溜めはやり直し)。
        /// </summary>
        /// <exception cref="ArgumentNullException">weaponId が null。</exception>
        /// <exception cref="KeyNotFoundException">weaponId が <see cref="Catalog"/> に無い。</exception>
        public WeaponSpec Equip(int playerId, string weaponId)
        {
            var spec = Catalog.Get(weaponId);
            _entries[playerId] = new Entry(spec, new ShotBudget(spec.FireInterval, _burst));
            return spec;
        }

        /// <summary>playerId が武器を持っていれば true とその武器。無ければ false と null。</summary>
        public bool TryGetEquipped(int playerId, out WeaponSpec spec)
        {
            if (_entries.TryGetValue(playerId, out var entry))
            {
                spec = entry.Spec;
                return true;
            }

            spec = null;
            return false;
        }

        /// <summary>playerId の武器を外す (退出時)。持っていたら true。</summary>
        public bool Remove(int playerId)
        {
            return _entries.Remove(playerId);
        }

        /// <summary>全員の武器を外す。</summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>全員の連射の制限を満タンからやり直す (次の試合の開始時)。持たせている武器はそのまま。</summary>
        public void ResetBudgets()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            var ids = new List<int>(_entries.Keys);
            foreach (var id in ids)
            {
                var spec = _entries[id].Spec;
                _entries[id] = new Entry(spec, new ShotBudget(spec.FireInterval, _burst));
            }
        }

        /// <summary>
        /// playerId が weaponId で撃ってよいか、武器だけを調べる (連射の制限は減らさない)。
        /// weaponId が <see cref="Catalog"/> に無ければ <see cref="ShotRejection.UnknownWeapon"/>、
        /// 武器を持っていなければ <see cref="ShotRejection.NoWeapon"/>、持っている武器と違えば <see cref="ShotRejection.WeaponMismatch"/>。
        /// よければ <see cref="ShotRejection.None"/> と持っている武器を返す。
        /// </summary>
        public ShotRejection CheckWeapon(int playerId, string weaponId, out WeaponSpec spec)
        {
            spec = null;
            if (!Catalog.Contains(weaponId))
            {
                return ShotRejection.UnknownWeapon;
            }

            if (!_entries.TryGetValue(playerId, out var entry))
            {
                return ShotRejection.NoWeapon;
            }

            if (!string.Equals(entry.Spec.Id, weaponId, StringComparison.Ordinal))
            {
                return ShotRejection.WeaponMismatch;
            }

            spec = entry.Spec;
            return ShotRejection.None;
        }

        /// <summary>
        /// <see cref="CheckWeapon"/> に通ったら、持っている武器の連射の制限から nowSeconds 時点で 1 発ぶん減らす。
        /// 間隔より速ければ <see cref="ShotRejection.FireRateExceeded"/>。武器が違う射撃では制限を減らさない。
        /// </summary>
        /// <param name="nowSeconds">サーバーの今の時刻 (秒)。有限値。</param>
        public ShotRejection TryFire(int playerId, string weaponId, double nowSeconds, out WeaponSpec spec)
        {
            var rejection = CheckWeapon(playerId, weaponId, out spec);
            if (rejection != ShotRejection.None)
            {
                return rejection;
            }

            if (!_entries[playerId].Budget.TryConsume(nowSeconds))
            {
                spec = null;
                return ShotRejection.FireRateExceeded;
            }

            return ShotRejection.None;
        }

        private readonly struct Entry
        {
            public Entry(WeaponSpec spec, ShotBudget budget)
            {
                Spec = spec;
                Budget = budget;
            }

            public WeaponSpec Spec { get; }

            public ShotBudget Budget { get; }
        }
    }
}
