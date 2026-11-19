using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class WeaponLoadout
    {
        public static Loadout<Weapon> Create(IReadOnlyList<WeaponSpec> specs)
        {
            return Create(specs, GameRules.Default);
        }

        public static Loadout<Weapon> Create(IReadOnlyList<WeaponSpec> specs, GameRules rules)
        {
            return Create(specs, rules, 0f);
        }

        public static Loadout<Weapon> Create(IReadOnlyList<WeaponSpec> specs, GameRules rules, float switchSeconds)
        {
            return Restore(specs, Array.Empty<WeaponAmmo>(), null, rules, switchSeconds, out _);
        }

        public static Loadout<Weapon> Restore(
            IReadOnlyList<WeaponSpec> specs,
            IEnumerable<WeaponAmmo> ammo,
            string equippedWeaponId,
            out IReadOnlyList<string> unknownWeaponIds)
        {
            return Restore(specs, ammo, equippedWeaponId, GameRules.Default, out unknownWeaponIds);
        }

        public static Loadout<Weapon> Restore(
            IReadOnlyList<WeaponSpec> specs,
            IEnumerable<WeaponAmmo> ammo,
            string equippedWeaponId,
            GameRules rules,
            out IReadOnlyList<string> unknownWeaponIds)
        {
            return Restore(specs, ammo, equippedWeaponId, rules, 0f, out unknownWeaponIds);
        }

        // ammo にある武器の予備弾はセーブの値をそのまま使い、GameRules.ReserveAmmoMultiplier を掛け直さない (作ったときに掛けた値がセーブされているため)。
        public static Loadout<Weapon> Restore(
            IReadOnlyList<WeaponSpec> specs,
            IEnumerable<WeaponAmmo> ammo,
            string equippedWeaponId,
            GameRules rules,
            float switchSeconds,
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

            var loadout = new Loadout<Weapon>(weapons, switchSeconds, Holster, w => w.Spec.DrawSeconds);

            // まだ誰も Switched を購読していないうちに選ぶので、呼ぶ側には持ち替えとして見えない
            var equipped = IndexOf(loadout, equippedWeaponId);
            if (equipped > 0)
            {
                loadout.TrySelect(equipped);

                // 作り直した時点で持っている武器を、持ち替え中にしないため
                loadout.Tick(loadout.SwitchSecondsTo(equipped));
            }

            unknownWeaponIds = unknown.AsReadOnly();
            return loadout;
        }

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

        public static bool TrySelect(this Loadout<Weapon> loadout, string weaponId)
        {
            var index = IndexOf(loadout, weaponId);
            return index >= 0 && loadout.TrySelect(index);
        }

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
