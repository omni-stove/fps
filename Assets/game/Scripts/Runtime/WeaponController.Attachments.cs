using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class WeaponController
    {
        public const string AttachmentsLogPrefix = "[Combat] attachments";

        private readonly Dictionary<string, IReadOnlyList<string>> _attachments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private ItemCatalog _attachmentCatalog;

        public ItemCatalog AttachmentCatalog
        {
            get => _attachmentCatalog ?? KitTuning.Items;
            set => _attachmentCatalog = value;
        }

        public IReadOnlyList<string> AttachmentsOf(string weaponId)
        {
            return weaponId != null && _attachments.TryGetValue(weaponId, out var attached) ? attached : WeaponAttachments.None;
        }

        public bool SetAttachments(string weaponId, IReadOnlyList<string> attachments)
        {
            EnsureInitialized();
            var index = _unarmed ? -1 : _loadout.IndexOf(weaponId);
            if (index < 0)
            {
                return false;
            }

            var catalog = AttachmentCatalog;
            var kept = WeaponAttachments.Keep(catalog, weaponId, attachments);
            if (WeaponAttachments.SameAs(kept, AttachmentsOf(weaponId)))
            {
                return true;
            }

            var spec = WeaponAttachments.Apply(_loadedDefinitions[index].ToSpec(), kept, catalog);
            var weapon = _loadout.Items[index];
            weapon.CancelReload();
            var magazine = weapon.AmmoInMagazine;
            var excess = magazine - spec.MagazineSize;
            var reserve = weapon.ReserveAmmo;
            if (excess > 0)
            {
                magazine = spec.MagazineSize;
                if (weapon.AmmoPool != null)
                {
                    weapon.AmmoPool.Put(excess);
                    reserve = weapon.ReserveAmmo;
                }
                else
                {
                    reserve += excess;
                }
            }

            var carried = new List<WeaponAmmo>(_loadout.SnapshotAmmo());
            carried[index] = new WeaponAmmo(weaponId, magazine, reserve);
            var specs = (WeaponSpec[])_specs.Clone();
            specs[index] = spec;
            var loadout = WeaponLoadout.Restore(specs, carried, Weapon.Spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, -1);
            RememberAttachments(weaponId, kept);
            SetLoadout(loadout, specs, _loadedDefinitions, _loadedRules);
            Debug.Log(
                $"{AttachmentsLogPrefix} weapon={weaponId} attached={WeaponAttachments.Describe(kept)} magazine={magazine}/{spec.MagazineSize} weight={spec.Weight:0.###}",
                this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public void RestoreLoadout(IEnumerable<WeaponAmmo> ammo, string equippedWeaponId, IReadOnlyDictionary<string, IReadOnlyList<string>> attachments)
        {
            EnsureInitialized();
            var catalog = AttachmentCatalog;
            var specs = new WeaponSpec[_loadedDefinitions.Length];
            _attachments.Clear();
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = _loadedDefinitions[i].ToSpec();
                var kept = attachments != null && attachments.TryGetValue(spec.Id, out var saved)
                    ? WeaponAttachments.Keep(catalog, spec.Id, saved)
                    : WeaponAttachments.None;
                RememberAttachments(spec.Id, kept);
                specs[i] = WeaponAttachments.Apply(spec, kept, catalog);
            }

            _specs = specs;
            RestoreLoadout(ammo, equippedWeaponId);
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> SnapshotAttachments()
        {
            EnsureInitialized();
            var snapshot = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (_unarmed)
            {
                return snapshot;
            }

            foreach (var weapon in _loadout.Items)
            {
                var attached = AttachmentsOf(weapon.Spec.Id);
                if (attached.Count > 0)
                {
                    snapshot[weapon.Spec.Id] = attached;
                }
            }

            return snapshot;
        }

        private void RememberAttachments(string weaponId, IReadOnlyList<string> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                _attachments.Remove(weaponId);
            }
            else
            {
                _attachments[weaponId] = attachments;
            }
        }
    }
}
