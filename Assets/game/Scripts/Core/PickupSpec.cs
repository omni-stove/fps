using System;

namespace Fps.Core
{
    /// <summary>
    /// 拾える物 1 つの中身。種類ごとの作り方 (<see cref="Weapon"/>・<see cref="ReserveAmmo"/>・<see cref="Health"/>・<see cref="Armor"/>・<see cref="Tag"/>) で作る。
    /// 拾ったときに何が起きるかは <see cref="PickupJudge.Judge"/> が拾う人の状態と合わせて決める。
    /// </summary>
    public sealed class PickupSpec
    {
        /// <summary><see cref="TagOwnerId"/> の、持ち主のいないときの値。</summary>
        public const int NoOwner = -1;

        private PickupSpec(PickupKind kind, int amount, string weaponId, int tagOwnerId, Side? tagOwnerSide, PickupEligibility eligibility)
        {
            Kind = kind;
            Amount = amount;
            WeaponId = weaponId;
            TagOwnerId = tagOwnerId;
            TagOwnerSide = tagOwnerSide;
            Eligibility = eligibility;
        }

        public PickupKind Kind { get; }

        /// <summary>
        /// 量。<see cref="PickupKind.Weapon"/> は既に持っていたときに足す予備弾 (0 なら持っていれば拾えない)、
        /// <see cref="PickupKind.ReserveAmmo"/> は予備弾の数、<see cref="PickupKind.Health"/> は回復量、<see cref="PickupKind.Armor"/> は防具の量、
        /// <see cref="PickupKind.Tag"/> は 1。
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// <see cref="PickupKind.Weapon"/> は渡す武器の Id。<see cref="PickupKind.ReserveAmmo"/> は弾を足す武器の Id で、null なら今持っている武器
        /// (満タンなら他の持ち武器)。それ以外の種類では null。
        /// </summary>
        public string WeaponId { get; }

        /// <summary><see cref="PickupKind.Tag"/> の持ち主 (倒れた人) のモードでの ID。分からないか他の種類なら <see cref="NoOwner"/>。</summary>
        public int TagOwnerId { get; }

        /// <summary><see cref="PickupKind.Tag"/> の持ち主の陣営。分からないか他の種類なら null。</summary>
        public Side? TagOwnerSide { get; }

        /// <summary>拾える陣営の条件。</summary>
        public PickupEligibility Eligibility { get; }

        /// <summary>武器。持っていなければ持ち武器に加え、持っていればその武器の予備弾を ammoIfOwned 足す。</summary>
        /// <param name="weaponId">渡す武器の Id。空でないこと。</param>
        /// <param name="ammoIfOwned">既に持っていたときに足す予備弾。0 以上。</param>
        /// <param name="eligibility">拾える陣営の条件。</param>
        public static PickupSpec Weapon(string weaponId, int ammoIfOwned, PickupEligibility eligibility = default)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                throw new ArgumentException("weaponId must not be null or empty", nameof(weaponId));
            }

            if (ammoIfOwned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoIfOwned), ammoIfOwned, "ammoIfOwned must not be negative");
            }

            return new PickupSpec(PickupKind.Weapon, ammoIfOwned, weaponId, NoOwner, null, eligibility);
        }

        /// <summary>予備弾を amount 足す。</summary>
        /// <param name="amount">足す数。1 以上。</param>
        /// <param name="weaponId">弾を足す武器の Id。null か空なら今持っている武器 (満タンなら他の持ち武器)。</param>
        /// <param name="eligibility">拾える陣営の条件。</param>
        public static PickupSpec ReserveAmmo(int amount, string weaponId = null, PickupEligibility eligibility = default)
        {
            return new PickupSpec(
                PickupKind.ReserveAmmo,
                RequirePositive(amount, nameof(amount)),
                string.IsNullOrEmpty(weaponId) ? null : weaponId,
                NoOwner,
                null,
                eligibility);
        }

        /// <summary>体力を amount 回復する。</summary>
        /// <param name="amount">回復量。1 以上。</param>
        /// <param name="eligibility">拾える陣営の条件。</param>
        public static PickupSpec Health(int amount, PickupEligibility eligibility = default)
        {
            return new PickupSpec(PickupKind.Health, RequirePositive(amount, nameof(amount)), null, NoOwner, null, eligibility);
        }

        /// <summary>防具を amount 足す。</summary>
        /// <param name="amount">足す量。1 以上。</param>
        /// <param name="eligibility">拾える陣営の条件。</param>
        public static PickupSpec Armor(int amount, PickupEligibility eligibility = default)
        {
            return new PickupSpec(PickupKind.Armor, RequirePositive(amount, nameof(amount)), null, NoOwner, null, eligibility);
        }

        /// <summary>倒れた人のタグ。</summary>
        /// <param name="ownerId">持ち主のモードでの ID。0 以上か <see cref="NoOwner"/>。</param>
        /// <param name="ownerSide">持ち主の陣営。分からなければ null。</param>
        /// <param name="eligibility">拾える陣営の条件。</param>
        public static PickupSpec Tag(int ownerId, Side? ownerSide, PickupEligibility eligibility = default)
        {
            if (ownerId < NoOwner)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), ownerId, "ownerId must be 0 or more, or NoOwner");
            }

            return new PickupSpec(PickupKind.Tag, 1, null, ownerId, ownerSide, eligibility);
        }

        public override string ToString()
        {
            return PickupLog.Describe(this);
        }

        private static int RequirePositive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive");
            }

            return value;
        }
    }
}
