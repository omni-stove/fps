using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// オフラインのプレイヤーが持つ <see cref="Weapon"/> の <see cref="Loadout{T}"/> を作り、武器の Id で引く。
    /// 持ち替えに時間はかけず (<see cref="Loadout{T}.SwitchSeconds"/> は 0)、しまう武器のリロードは取り消す。
    /// </summary>
    public static class WeaponLoadout
    {
        /// <summary>specs の武器を 1 本ずつ満タンで持ち、0 番を持った並びを作る。予備弾は <see cref="GameRules.Default"/> (有限・倍率 1)。</summary>
        /// <param name="specs">持つ武器。1 本以上で、null と重複した Id を含まない。</param>
        /// <exception cref="ArgumentNullException">specs が null。</exception>
        /// <exception cref="ArgumentException">specs が空、null を含む、または Id が重複している。</exception>
        public static Loadout<Weapon> Create(IReadOnlyList<WeaponSpec> specs)
        {
            return Create(specs, GameRules.Default);
        }

        /// <summary>
        /// specs の武器を 1 本ずつ満タンで持ち、0 番を持った並びを作る。予備弾は <see cref="WeaponSpec.ReserveAmmo"/> に
        /// <see cref="GameRules.ReserveAmmoMultiplier"/> を掛けた数で、<see cref="GameRules.InfiniteReserve"/> なら減らない。
        /// </summary>
        /// <param name="specs">持つ武器。1 本以上で、null と重複した Id を含まない。</param>
        /// <param name="rules">予備弾のルール。</param>
        /// <exception cref="ArgumentNullException">specs か rules が null。</exception>
        /// <exception cref="ArgumentException">specs が空、null を含む、または Id が重複している。</exception>
        public static Loadout<Weapon> Create(IReadOnlyList<WeaponSpec> specs, GameRules rules)
        {
            return Restore(specs, Array.Empty<WeaponAmmo>(), null, rules, out _);
        }

        /// <summary>
        /// <see cref="Restore(IReadOnlyList{WeaponSpec}, IEnumerable{WeaponAmmo}, string, GameRules, out IReadOnlyList{string})"/> を
        /// <see cref="GameRules.Default"/> (有限・倍率 1) で呼ぶ。
        /// </summary>
        public static Loadout<Weapon> Restore(
            IReadOnlyList<WeaponSpec> specs,
            IEnumerable<WeaponAmmo> ammo,
            string equippedWeaponId,
            out IReadOnlyList<string> unknownWeaponIds)
        {
            return Restore(specs, ammo, equippedWeaponId, GameRules.Default, out unknownWeaponIds);
        }

        /// <summary>
        /// specs の武器を持ち、Id が ammo にある武器はその弾数で、無い武器は満タンで作る。弾倉の容量を超える分は容量に揃える。
        /// equippedWeaponId の武器を持った状態で返し、その Id が specs に無い (空を含む) なら 0 番を持つ。
        /// ammo にある武器の予備弾はセーブの値をそのまま使い、<see cref="GameRules.ReserveAmmoMultiplier"/> を掛け直さない
        /// (作ったときに掛けた値がセーブされているため)。ammo に無い武器は <see cref="Create(IReadOnlyList{WeaponSpec}, GameRules)"/> と同じく倍率を掛ける。
        /// </summary>
        /// <param name="specs">持つ武器。1 本以上で、null と重複した Id を含まない。</param>
        /// <param name="ammo">武器ごとの残弾 (セーブデータ)。null を含まない。同じ Id が複数あれば後のものを使う。</param>
        /// <param name="equippedWeaponId">持っていた武器の Id。null か空なら 0 番。</param>
        /// <param name="rules">予備弾のルール。</param>
        /// <param name="unknownWeaponIds">ammo にあるが specs に無かった武器の Id (ロードアウトが変わったセーブ)。呼ぶ側が警告に使う。</param>
        /// <exception cref="ArgumentNullException">specs・ammo・rules のいずれかが null。</exception>
        /// <exception cref="ArgumentException">specs が空、null を含む、Id が重複している、または ammo が null を含む。</exception>
        public static Loadout<Weapon> Restore(
            IReadOnlyList<WeaponSpec> specs,
            IEnumerable<WeaponAmmo> ammo,
            string equippedWeaponId,
            GameRules rules,
            out IReadOnlyList<string> unknownWeaponIds)
        {
            if (specs == null)
            {
                throw new ArgumentNullException(nameof(specs));
            }

            if (ammo == null)
            {
                throw new ArgumentNullException(nameof(ammo));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (specs.Count == 0)
            {
                throw new ArgumentException("specs must not be empty", nameof(specs));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < specs.Count; i++)
            {
                if (specs[i] == null)
                {
                    throw new ArgumentException("specs must not contain null (index " + i + ")", nameof(specs));
                }

                if (!ids.Add(specs[i].Id))
                {
                    // Id で持ち替え先と残弾を引くので、同じ Id が 2 本あるとどちらか決められない
                    throw new ArgumentException("specs has more than one weapon with id '" + specs[i].Id + "'", nameof(specs));
                }
            }

            var saved = new Dictionary<string, WeaponAmmo>(StringComparer.Ordinal);
            var unknown = new List<string>();
            foreach (var entry in ammo)
            {
                if (entry == null)
                {
                    throw new ArgumentException("ammo must not contain null", nameof(ammo));
                }

                saved[entry.WeaponId] = entry;
                if (!ids.Contains(entry.WeaponId) && !unknown.Contains(entry.WeaponId))
                {
                    unknown.Add(entry.WeaponId);
                }
            }

            var weapons = new Weapon[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                weapons[i] = saved.TryGetValue(spec.Id, out var entry)
                    ? new Weapon(spec, Math.Min(entry.AmmoInMagazine, spec.MagazineSize), entry.ReserveAmmo, rules)
                    : new Weapon(spec, rules);
            }

            var loadout = new Loadout<Weapon>(weapons, 0f, Holster);

            // まだ誰も Switched を購読していないうちに選ぶので、呼ぶ側には持ち替えとして見えない
            var equipped = IndexOf(loadout, equippedWeaponId);
            if (equipped > 0)
            {
                loadout.TrySelect(equipped);
            }

            unknownWeaponIds = unknown.AsReadOnly();
            return loadout;
        }

        /// <summary>Id が weaponId の武器の番号。無い (null・空を含む) なら -1。</summary>
        public static int IndexOf(this Loadout<Weapon> loadout, string weaponId)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (string.IsNullOrEmpty(weaponId))
            {
                return -1;
            }

            for (var i = 0; i < loadout.Count; i++)
            {
                if (string.Equals(loadout.Items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Id が weaponId の武器に持ち替える。無いか今持っている武器なら何もせず false。</summary>
        public static bool TrySelect(this Loadout<Weapon> loadout, string weaponId)
        {
            var index = IndexOf(loadout, weaponId);
            return index >= 0 && loadout.TrySelect(index);
        }

        /// <summary>持っている全武器の今の残弾を、並びの順に写す。セーブに使う。</summary>
        public static IReadOnlyList<WeaponAmmo> SnapshotAmmo(this Loadout<Weapon> loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var result = new WeaponAmmo[loadout.Count];
            for (var i = 0; i < loadout.Count; i++)
            {
                result[i] = WeaponAmmo.Of(loadout.Items[i]);
            }

            return result;
        }

        private static void Holster(Weapon weapon)
        {
            weapon.CancelReload();
        }
    }
}
