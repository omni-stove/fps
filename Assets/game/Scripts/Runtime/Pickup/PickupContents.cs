using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 武器の中身は Core では Id だけで足りるが、持ち武器に加えるには定義のアセットが要るので Weapon に持つ。
    [Serializable]
    public sealed class PickupContents
    {
        public const int DefaultHealthAmount = 25;

        public const int NoCarriedAmmo = -1;

        [SerializeField, Tooltip("種類")]
        private PickupKind _kind = PickupKind.Health;

        [SerializeField, Min(0), Tooltip("量。武器は持っていたときに足す予備弾、予備弾は数、回復・防具は量、アイテムは数。タグでは使わない")]
        private int _amount = DefaultHealthAmount;

        [SerializeField, Tooltip("武器は渡す武器。予備弾は弾を足す武器で、空なら今持っている武器")]
        private WeaponDefinition _weapon;

        [SerializeField, Min(NoCarriedAmmo), Tooltip("武器を誰かが落としたときの弾倉の弾数。拾った人はこの弾数で持つ。-1 なら満タンで持つ")]
        private int _carriedMagazine = NoCarriedAmmo;

        [SerializeField, Min(0), Tooltip("武器を誰かが落としたときの予備弾。弾倉の弾数が -1 なら使わない")]
        private int _carriedReserve;

        [SerializeField, Tooltip("武器に付いている物 (持ち物のアイテムの Id、Tuning/inventory.json の items の id)。拾った人の武器に付く")]
        private string[] _attachments;

        [SerializeField, Tooltip("アイテムは渡すアイテムの Id (Tuning/inventory.json の items の id)")]
        private string _itemId;

        [SerializeField, Tooltip("拾える陣営の絞り方")]
        private PickupSideRule _sideRule = PickupSideRule.Anyone;

        [SerializeField, Min(0), Tooltip("絞り方が使う陣営の ID")]
        private int _sideId;

        [SerializeField, Min(PickupSpec.NoOwner), Tooltip("タグの持ち主のモードでの ID。-1 なら分からない")]
        private int _tagOwnerId = PickupSpec.NoOwner;

        [SerializeField, Min(-1), Tooltip("タグの持ち主の陣営の ID。-1 なら分からない")]
        private int _tagOwnerSideId = -1;

        public PickupKind Kind => _kind;

        public int Amount => _amount;

        public WeaponDefinition Weapon => _weapon;

        public WeaponAmmo CarriedAmmo =>
            _kind == PickupKind.Weapon && _weapon != null && _carriedMagazine >= 0
                ? new WeaponAmmo(_weapon.Id, _carriedMagazine, Math.Max(0, _carriedReserve))
                : null;

        public IReadOnlyList<string> Attachments =>
            _kind == PickupKind.Weapon && _attachments != null && _attachments.Length > 0 ? _attachments : WeaponAttachments.None;

        public string ItemId => _kind == PickupKind.Item ? _itemId : null;

        public PickupEligibility Eligibility => PickupEligibility.Of(_sideRule, new Side(Math.Max(0, _sideId)));

        public static PickupContents ForWeapon(WeaponDefinition weapon, int ammoIfOwned, PickupEligibility eligibility = default)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return Create(PickupKind.Weapon, ammoIfOwned, weapon, eligibility, PickupSpec.NoOwner, null);
        }

        public static PickupContents ForDroppedWeapon(
            WeaponDefinition weapon, WeaponAmmo ammo, PickupEligibility eligibility = default, IReadOnlyList<string> attachments = null)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (ammo == null)
            {
                throw new ArgumentNullException(nameof(ammo));
            }

            if (!string.Equals(ammo.WeaponId, weapon.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"ammo is for weapon '{ammo.WeaponId}', not '{weapon.Id}'", nameof(ammo));
            }

            var contents = Create(PickupKind.Weapon, ammo.AmmoInMagazine + ammo.ReserveAmmo, weapon, eligibility, PickupSpec.NoOwner, null);
            contents._carriedMagazine = ammo.AmmoInMagazine;
            contents._carriedReserve = ammo.ReserveAmmo;
            contents._attachments = ToArray(attachments);
            return contents;
        }

        public static PickupContents ForReserveAmmo(int amount, WeaponDefinition weapon = null, PickupEligibility eligibility = default)
        {
            return Create(PickupKind.ReserveAmmo, amount, weapon, eligibility, PickupSpec.NoOwner, null);
        }

        public static PickupContents ForHealth(int amount, PickupEligibility eligibility = default)
        {
            return Create(PickupKind.Health, amount, null, eligibility, PickupSpec.NoOwner, null);
        }

        public static PickupContents ForArmor(int amount, PickupEligibility eligibility = default)
        {
            return Create(PickupKind.Armor, amount, null, eligibility, PickupSpec.NoOwner, null);
        }

        public static PickupContents ForItem(string itemId, int amount, PickupEligibility eligibility = default)
        {
            var contents = new PickupContents
            {
                _kind = PickupKind.Item,
                _amount = amount,
                _itemId = itemId,
                _sideRule = eligibility.Rule,
                _sideId = eligibility.Side.Id,
            };
            contents.ToSpec();
            return contents;
        }

        public PickupContents WithAttachments(IReadOnlyList<string> attachments)
        {
            if (_kind != PickupKind.Weapon)
            {
                throw new ArgumentException("only a Weapon pickup carries attachments", nameof(attachments));
            }

            var copy = Clone();
            copy._attachments = ToArray(attachments);
            return copy;
        }

        public PickupContents WithAmount(int amount)
        {
            var copy = Clone();
            copy._amount = amount;
            copy.ToSpec();
            return copy;
        }

        public static PickupContents ForTag(int ownerId, Side? ownerSide, PickupEligibility eligibility = default)
        {
            return Create(PickupKind.Tag, 1, null, eligibility, ownerId, ownerSide);
        }

        public PickupSpec ToSpec()
        {
            var eligibility = Eligibility;
            switch (_kind)
            {
                case PickupKind.Weapon:
                    if (_weapon == null)
                    {
                        throw new ArgumentException("a Weapon pickup needs a WeaponDefinition");
                    }

                    return PickupSpec.Weapon(_weapon.Id, _amount, eligibility, _weapon.Weight + AttachedWeight());
                case PickupKind.ReserveAmmo:
                    return PickupSpec.ReserveAmmo(_amount, _weapon != null ? _weapon.Id : null, eligibility);
                case PickupKind.Health:
                    return PickupSpec.Health(_amount, eligibility);
                case PickupKind.Armor:
                    return PickupSpec.Armor(_amount, eligibility);
                case PickupKind.Tag:
                    return PickupSpec.Tag(
                        Math.Max(PickupSpec.NoOwner, _tagOwnerId),
                        _tagOwnerSideId >= 0 ? new Side(_tagOwnerSideId) : (Side?)null,
                        eligibility);
                case PickupKind.Item:
                    return PickupSpec.Item(_itemId, _amount, eligibility);
                default:
                    throw new ArgumentException($"unknown PickupKind {_kind}");
            }
        }

        public PickupContents Clone()
        {
            var copy = (PickupContents)MemberwiseClone();
            copy._attachments = _attachments != null ? (string[])_attachments.Clone() : null;
            return copy;
        }

        // 持ち物に入るかは付いた物ごとの重さで見るので、武器の重さに付いた物の重さを足す
        private float AttachedWeight()
        {
            var catalog = KitTuning.Items;
            var weight = 0f;
            foreach (var itemId in WeaponAttachments.Keep(catalog, _weapon.Id, Attachments))
            {
                if (catalog.TryFind(itemId, out var item))
                {
                    weight += item.Weight;
                }
            }

            return weight;
        }

        private static string[] ToArray(IReadOnlyList<string> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return null;
            }

            var copy = new string[attachments.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = attachments[i];
            }

            return copy;
        }

        public override string ToString()
        {
            try
            {
                return PickupLog.Describe(ToSpec());
            }
            catch (ArgumentException e)
            {
                return $"kind={_kind} (invalid: {e.Message})";
            }
        }

        private static PickupContents Create(
            PickupKind kind,
            int amount,
            WeaponDefinition weapon,
            PickupEligibility eligibility,
            int tagOwnerId,
            Side? tagOwnerSide)
        {
            var contents = new PickupContents
            {
                _kind = kind,
                _amount = amount,
                _weapon = weapon,
                _sideRule = eligibility.Rule,
                _sideId = eligibility.Side.Id,
                _tagOwnerId = tagOwnerId,
                _tagOwnerSideId = tagOwnerSide.HasValue ? tagOwnerSide.Value.Id : -1,
            };

            // 作った時点で不正な値を知らせ、シーンに保存されてから Play で気づくのを避ける。
            contents.ToSpec();
            return contents;
        }
    }
}
