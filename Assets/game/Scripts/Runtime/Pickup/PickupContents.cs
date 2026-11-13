using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Scene と Inspector に置ける拾える物の中身。<see cref="ToSpec"/> で Core の <see cref="PickupSpec"/> にする。
    /// 武器の中身は Core では Id だけで足りるが、持ち武器に加えるには定義のアセットが要るので <see cref="Weapon"/> に持つ。
    /// 作るときは種類ごとの作り方 (<see cref="ForWeapon"/> など) を使い、シーン生成スクリプトが <see cref="Pickup"/> や
    /// <see cref="PickupSpawnPoint"/>・<see cref="DropOnDeath"/> に配線する。
    /// </summary>
    [Serializable]
    public sealed class PickupContents
    {
        public const int DefaultHealthAmount = 25;

        [SerializeField, Tooltip("種類")]
        private PickupKind _kind = PickupKind.Health;

        [SerializeField, Min(0), Tooltip("量。武器は持っていたときに足す予備弾、予備弾は数、回復・防具は量。タグでは使わない")]
        private int _amount = DefaultHealthAmount;

        [SerializeField, Tooltip("武器は渡す武器。予備弾は弾を足す武器で、空なら今持っている武器")]
        private WeaponDefinition _weapon;

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

        /// <summary>渡す武器か、弾を足す武器の定義。無ければ null。</summary>
        public WeaponDefinition Weapon => _weapon;

        public PickupEligibility Eligibility => PickupEligibility.Of(_sideRule, new Side(Math.Max(0, _sideId)));

        /// <summary>武器。持っていなければ持ち武器に加え、持っていれば予備弾を ammoIfOwned 足す。</summary>
        public static PickupContents ForWeapon(WeaponDefinition weapon, int ammoIfOwned, PickupEligibility eligibility = default)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return Create(PickupKind.Weapon, ammoIfOwned, weapon, eligibility, PickupSpec.NoOwner, null);
        }

        /// <summary>予備弾。weapon が null なら今持っている武器に足す。</summary>
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

        /// <summary>倒れた人のタグ。</summary>
        /// <param name="ownerId">持ち主のモードでの ID。分からなければ <see cref="PickupSpec.NoOwner"/>。</param>
        /// <param name="ownerSide">持ち主の陣営。分からなければ null。</param>
        public static PickupContents ForTag(int ownerId, Side? ownerSide, PickupEligibility eligibility = default)
        {
            return Create(PickupKind.Tag, 1, null, eligibility, ownerId, ownerSide);
        }

        /// <summary>
        /// Core の中身にする。値が中身の種類に合わなければ (武器の定義が無い・量が 0 など) <see cref="ArgumentException"/>、
        /// 武器の定義の Id が空なら <see cref="ArgumentException"/> を投げる。
        /// </summary>
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

                    return PickupSpec.Weapon(_weapon.Id, _amount, eligibility);
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
                default:
                    throw new ArgumentException($"unknown PickupKind {_kind}");
            }
        }

        /// <summary>同じ中身の写し。落とすたびに別の物として持たせるのに使う。</summary>
        public PickupContents Clone()
        {
            return (PickupContents)MemberwiseClone();
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
