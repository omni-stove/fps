using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
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

        public event Action<IReadOnlyList<string>> AttachmentsReleased;

        // 作り直しで付けた物を黙って捨てると持ち物から物が消えるので、先に持ち主へ知らせる
        private void ReleaseAttachments()
        {
            if (_attachments.Count > 0 && AttachmentsReleased != null)
            {
                var released = new List<string>();
                foreach (var attached in _attachments.Values)
                {
                    released.AddRange(attached);
                }

                AttachmentsReleased(released);
            }

            _attachments.Clear();
        }

        // 持ち物へ戻さずに外すのは、持ち物を最初の状態に戻した後 (出直し) に呼ばれ、付けた物が持ち物と武器の両方に残って増えるのを防ぐため。
        public void ClearAttachments()
        {
            if (_attachments.Count == 0)
            {
                return;
            }

            EnsureInitialized();
            _attachments.Clear();
            _specs = ToSpecs(_loadedDefinitions);
            if (_unarmed)
            {
                return;
            }

            var loadout = WeaponLoadout.Restore(_specs, _loadout.SnapshotAmmo(), Weapon.Spec.Id, _loadedRules, _switchSeconds, out _);
            KeepAmmoTypes(loadout, -1);
            SetLoadout(loadout, _specs, _loadedDefinitions, _loadedRules);
            Debug.Log($"{AttachmentsLogPrefix} cleared weapon={Weapon.Spec.Id}", this);
            WeaponChanged?.Invoke(Weapon);
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
            var magazine = weapon.AmmoInMagazine;
            var excess = magazine - spec.MagazineSize;
            // 弾倉が減って溢れる弾が持ち物に入らなければ、弾を失わないよう付け外しを断る
            if (excess > 0 && weapon.AmmoPool != null && weapon.AmmoPool.Room < excess)
            {
                Debug.Log($"{AttachmentsLogPrefix} weapon={weaponId} attached={WeaponAttachments.Describe(kept)} rejected=NoRoomForAmmo excess={excess}", this);
                return false;
            }

            weapon.CancelReload();
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
            var loadout = WeaponLoadout.Restore(specs, carried, Weapon.Spec.Id, _loadedRules, _switchSeconds, out _);
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

            if (AttachmentsRestored != null)
            {
                var restored = new List<string>();
                foreach (var attached in _attachments.Values)
                {
                    restored.AddRange(attached);
                }

                if (restored.Count > 0)
                {
                    AttachmentsRestored(restored);
                }
            }
        }

        public event Action<IReadOnlyList<string>> AttachmentsRestored;

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
