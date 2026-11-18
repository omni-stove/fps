using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        public int MaxWeapons => _rules.MaxWeapons;

        public Func<WeaponSpec, IAmmoPool> AmmoPools { get; private set; }

        // 持ち武器の並びを作り直したときに、予備弾の受け皿をつなぐ前に呼ぶ。持ち物が武器の場所を、受け皿に入る弾薬より先に取れるようにするため
        public event Action LoadoutReplaced;

        public bool SelectNext()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectNext();
        }

        public bool SelectPrevious()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectPrevious();
        }

        public bool Select(int index)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(index);
        }

        public bool Select(string weaponId)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(weaponId);
        }

        public bool Disarm()
        {
            EnsureInitialized();
            if (_unarmed)
            {
                return false;
            }

            _loadout.Current.CancelReload();
            _unarmed = true;
            Debug.Log(DisarmedLog, this);
            WeaponChanged?.Invoke(null);
            return true;
        }

        public IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId)
        {
            EnsureInitialized();
            if (_unarmed)
            {
                currentWeaponId = null;
                return Array.Empty<PickupWeaponState>();
            }

            var items = _loadout.Items;
            var list = new PickupWeaponState[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                list[i] = PickupWeaponState.Of(items[i]);
            }

            currentWeaponId = _loadout.Current.Spec.Id;
            return list;
        }

        public void ApplyRules(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            EnsureInitialized();
            var loadout = WeaponLoadout.Create(_specs, rules);
            _rules = rules;
            SetLoadout(loadout, _specs, _loadedDefinitions, rules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        public void RestoreLoadout(IEnumerable<WeaponAmmo> ammo, string equippedWeaponId)
        {
            EnsureInitialized();
            _unarmed = false;

            var loadout = WeaponLoadout.Restore(_specs, ammo, equippedWeaponId, _loadedRules, out var unknown);
            foreach (var id in unknown)
            {
                Debug.LogWarning($"[WeaponController] '{name}' は武器 '{id}' を持っていないため、セーブされたその残弾を捨てます。", this);
            }

            if (!string.IsNullOrEmpty(equippedWeaponId) && loadout.IndexOf(equippedWeaponId) < 0)
            {
                Debug.LogWarning(
                    $"[WeaponController] '{name}' はセーブで持っていた武器 '{equippedWeaponId}' を持っていないため、{loadout.Current.Spec.LogName} を持ちます。",
                    this);
            }

            SetLoadout(loadout, _specs, _loadedDefinitions, _loadedRules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        // 持てる数の上限 (MaxWeapons) はここでは見ない。上限で持ち替えるのは拾う側 (Pickup) が SwapWeapon で行う。
        public bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed)
            {
                var only = new[] { spec };
                var armed = WeaponLoadout.Restore(only, AmmoList(ammo), spec.Id, _loadedRules, out _);
                _unarmed = false;
                _attachments.Clear();
                RememberAttachments(spec.Id, kept);
                SetLoadout(armed, only, new[] { definition }, _loadedRules);
                Debug.Log($"[Combat] weapon added index=0 id={spec.Id} name={spec.LogName}", this);
                WeaponChanged?.Invoke(Weapon);
                return true;
            }

            if (_loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var specs = new WeaponSpec[_specs.Length + 1];
            Array.Copy(_specs, specs, _specs.Length);
            specs[_specs.Length] = spec;
            var definitions = new WeaponDefinition[_loadedDefinitions.Length + 1];
            Array.Copy(_loadedDefinitions, definitions, _loadedDefinitions.Length);
            definitions[_loadedDefinitions.Length] = definition;

            var carried = new List<WeaponAmmo>(_loadout.SnapshotAmmo());
            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, Weapon.Spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, -1);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon added index={specs.Length - 1} id={spec.Id} name={spec.LogName} attachments={WeaponAttachments.Describe(kept)}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public bool SwapWeapon(
            string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dropped = default;
            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed || _loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var index = _loadout.IndexOf(droppedWeaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            var leavingAmmo = WeaponAmmo.Leaving(leaving);
            var leavingDefinition = _loadedDefinitions[index];
            var leavingAttachments = AttachmentsOf(leavingAmmo.WeaponId);

            var specs = (WeaponSpec[])_specs.Clone();
            specs[index] = spec;
            var definitions = (WeaponDefinition[])_loadedDefinitions.Clone();
            definitions[index] = definition;

            var carried = new List<WeaponAmmo>();
            foreach (var entry in _loadout.SnapshotAmmo())
            {
                if (!string.Equals(entry.WeaponId, leavingAmmo.WeaponId, StringComparison.Ordinal))
                {
                    carried.Add(entry);
                }
            }

            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, index);
            _attachments.Remove(leavingAmmo.WeaponId);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            dropped = new HeldWeapon(leavingDefinition, leavingAmmo, leavingAttachments);
            Debug.Log(
                $"[Combat] weapon swapped index={index} id={spec.Id} name={spec.LogName} dropped={leavingAmmo.WeaponId} "
                + $"dropped-ammo={leavingAmmo.AmmoInMagazine}/{leavingAmmo.ReserveAmmo} dropped-attachments={WeaponAttachments.Describe(leavingAttachments)}",
                this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public bool DropWeapon(string weaponId, out HeldWeapon dropped)
        {
            dropped = default;
            EnsureInitialized();
            var index = _unarmed ? -1 : _loadout.IndexOf(weaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            dropped = new HeldWeapon(_loadedDefinitions[index], WeaponAmmo.Leaving(leaving), AttachmentsOf(leaving.Spec.Id));
            _attachments.Remove(leaving.Spec.Id);
            if (_loadout.Count == 1)
            {
                // 持ち物の山は持ち武器から作るので、武器を持っていない状態でも並びを残したまま Disarm で手放したことにする
                leaving.UseAmmoPool(null);
                Debug.Log($"[Combat] weapon dropped index=0 id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
                Disarm();
                return true;
            }

            var specs = new WeaponSpec[_specs.Length - 1];
            var definitions = new WeaponDefinition[_loadedDefinitions.Length - 1];
            var carried = new List<WeaponAmmo>();
            var ammo = _loadout.SnapshotAmmo();
            for (int from = 0, to = 0; from < _specs.Length; from++)
            {
                if (from == index)
                {
                    continue;
                }

                specs[to] = _specs[from];
                definitions[to] = _loadedDefinitions[from];
                carried.Add(ammo[from]);
                to++;
            }

            var equipped = index == _loadout.CurrentIndex ? null : Weapon.Spec.Id;
            var loadout = WeaponLoadout.Restore(specs, carried, equipped, _loadedRules, out _);
            for (var i = 0; i < loadout.Count; i++)
            {
                loadout.Items[i].AmmoTypes.TrySelect(_loadout.Items[i < index ? i : i + 1].AmmoTypes.Index);
            }

            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon dropped index={index} id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        // 武器ごとの予備弾の受け皿 (持ち物の弾薬) を決め、今の武器と、この後に作り直す武器に使う。null で外す
        public void UseAmmoPools(Func<WeaponSpec, IAmmoPool> poolFor)
        {
            AmmoPools = poolFor;
            if (_loadout == null || _unarmed)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.UseAmmoPool(poolFor?.Invoke(weapon.Spec));
            }
        }

        // 残弾の数だけでは種類が戻るので、込めた弾が別の種類に変わらないよう作り直した武器へ選択を移す。replacedIndex の武器は入れ替えたので移さない。
        private void KeepAmmoTypes(Loadout<Weapon> rebuilt, int replacedIndex)
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                if (i != replacedIndex)
                {
                    rebuilt.Items[i].AmmoTypes.TrySelect(_loadout.Items[i].AmmoTypes.Index);
                }
            }
        }

        private static void RequireAmmoFor(WeaponSpec spec, WeaponAmmo ammo)
        {
            if (ammo != null && !string.Equals(ammo.WeaponId, spec.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"ammo is for weapon '{ammo.WeaponId}', not '{spec.Id}'", nameof(ammo));
            }
        }

        private static IEnumerable<WeaponAmmo> AmmoList(WeaponAmmo ammo)
        {
            return ammo != null ? new[] { ammo } : Array.Empty<WeaponAmmo>();
        }

        public int AddReserveAmmo(string weaponId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            EnsureInitialized();
            if (_unarmed)
            {
                return 0;
            }

            var index = _loadout.IndexOf(weaponId);
            return index >= 0 ? _loadout.Items[index].AddReserveAmmo(amount) : 0;
        }

        public IReadOnlyList<WeaponAmmo> SnapshotAmmo()
        {
            EnsureInitialized();
            return _unarmed ? Array.Empty<WeaponAmmo>() : _loadout.SnapshotAmmo();
        }

        private void SetLoadout(Loadout<Weapon> loadout, WeaponSpec[] specs, WeaponDefinition[] definitions, GameRules rules, bool resetAmmo = false)
        {
            if (_loadout != null)
            {
                _loadout.Switched -= OnSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Reloaded -= OnReloaded;
                    weapon.UseAmmoPool(null);
                }
            }

            _loadout = loadout;
            _specs = specs;
            _loadedDefinitions = definitions;
            _loadedRules = rules;
            LoadoutReplaced?.Invoke();
            if (_loadout == null)
            {
                return;
            }

            Dictionary<string, (IAmmoPool Pool, int Count)> reset = null;
            _loadout.Switched += OnSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.Reloaded += OnReloaded;

                // 武器を持っていない間の並びは手放した武器なので、その予備弾を持ち物に足さない
                if (AmmoPools == null || _unarmed)
                {
                    continue;
                }

                var own = weapon.ReserveAmmo;
                var pool = AmmoPools(weapon.Spec);
                if (weapon.UseAmmoPool(pool) && resetAmmo)
                {
                    reset ??= new Dictionary<string, (IAmmoPool Pool, int Count)>(StringComparer.Ordinal);
                    reset[pool.Id] = reset.TryGetValue(pool.Id, out var target) ? (pool, Math.Max(target.Count, own)) : (pool, own);
                }
            }

            // 受け皿につなぐと足りない分を足すだけなので、作り直しで予備弾が減る (難易度で減らす・セーブの値に戻す) ときは多い分を減らす
            if (reset != null)
            {
                foreach (var (pool, count) in reset.Values)
                {
                    var extra = pool.Count - count;
                    if (extra > 0)
                    {
                        pool.Take(extra);
                    }
                }
            }
        }

        private void OnSwitched(int index)
        {
            var spec = _loadout.Current.Spec;
            Debug.Log($"{SwitchedLogPrefix} index={index} id={spec.Id} name={spec.LogName}", this);
            WeaponChanged?.Invoke(Weapon);
        }

        private void OnReloaded()
        {
            Debug.Log(ReloadedLog, this);
        }

        private static WeaponDefinition[] ToArray(IReadOnlyList<WeaponDefinition> definitions)
        {
            var result = new WeaponDefinition[definitions.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = definitions[i];
            }

            return result;
        }

        private static WeaponSpec[] ToSpecs(WeaponDefinition[] definitions)
        {
            if (definitions.Length == 0)
            {
                throw new ArgumentException("definitions must not be empty", nameof(definitions));
            }

            var specs = new WeaponSpec[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null)
                {
                    throw new ArgumentException("definitions must not contain null (index " + i + ")", nameof(definitions));
                }

                specs[i] = definitions[i].ToSpec();
            }

            return specs;
        }
    }
}
