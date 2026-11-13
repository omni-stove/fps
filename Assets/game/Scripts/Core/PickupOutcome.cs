namespace Fps.Core
{
    /// <summary>
    /// 拾ったときに何が起きるか (<see cref="PickupJudge.Judge"/> の結果)。拾えたときは、拾う人に反映する量だけを持つ
    /// (上限で切り詰めた後の値)。拾えなければ <see cref="Rejection"/> に理由を持ち、量はすべて 0。
    /// </summary>
    public readonly struct PickupOutcome
    {
        private PickupOutcome(
            PickupKind kind,
            PickupRejection rejection,
            string weaponId,
            bool addsWeapon,
            int reserveAmmo,
            int health,
            int armor,
            int tagOwnerId,
            Side? tagOwnerSide,
            bool isOwnSideTag)
        {
            Kind = kind;
            Rejection = rejection;
            WeaponId = weaponId;
            AddsWeapon = addsWeapon;
            ReserveAmmo = reserveAmmo;
            Health = health;
            Armor = armor;
            TagOwnerId = tagOwnerId;
            TagOwnerSide = tagOwnerSide;
            IsOwnSideTag = isOwnSideTag;
        }

        /// <summary>拾った (拾おうとした) 物の種類。</summary>
        public PickupKind Kind { get; }

        /// <summary>拾えなかった理由。拾えたら <see cref="PickupRejection.None"/>。</summary>
        public PickupRejection Rejection { get; }

        /// <summary>拾えたら true。拾えた物は消える。</summary>
        public bool IsCollected => Rejection == PickupRejection.None;

        /// <summary>持ち武器に加える武器か、予備弾を足す武器の Id。どちらでもなければ null。</summary>
        public string WeaponId { get; }

        /// <summary><see cref="WeaponId"/> の武器を持ち武器に加えるなら true。</summary>
        public bool AddsWeapon { get; }

        /// <summary><see cref="WeaponId"/> の武器に足す予備弾。</summary>
        public int ReserveAmmo { get; }

        /// <summary>回復する体力。</summary>
        public int Health { get; }

        /// <summary>足す防具。</summary>
        public int Armor { get; }

        /// <summary>拾ったタグの持ち主のモードでの ID。タグでなければ <see cref="PickupSpec.NoOwner"/>。</summary>
        public int TagOwnerId { get; }

        /// <summary>拾ったタグの持ち主の陣営。タグでないか分からなければ null。</summary>
        public Side? TagOwnerSide { get; }

        /// <summary>
        /// 拾ったタグが拾った人と同じ陣営のものなら true (味方のタグを回収した)。持ち主か拾った人の陣営が分からなければ false。
        /// </summary>
        public bool IsOwnSideTag { get; }

        /// <summary>kind の物を rejection の理由で拾えなかった結果。rejection が <see cref="PickupRejection.None"/> なら <see cref="System.ArgumentException"/>。</summary>
        public static PickupOutcome Rejected(PickupKind kind, PickupRejection rejection)
        {
            if (rejection == PickupRejection.None)
            {
                throw new System.ArgumentException("rejection must not be None", nameof(rejection));
            }

            return new PickupOutcome(kind, rejection, null, false, 0, 0, 0, PickupSpec.NoOwner, null, false);
        }

        internal static PickupOutcome WeaponAdded(string weaponId)
        {
            return new PickupOutcome(PickupKind.Weapon, PickupRejection.None, weaponId, true, 0, 0, 0, PickupSpec.NoOwner, null, false);
        }

        internal static PickupOutcome AmmoAdded(PickupKind kind, string weaponId, int amount)
        {
            return new PickupOutcome(kind, PickupRejection.None, weaponId, false, amount, 0, 0, PickupSpec.NoOwner, null, false);
        }

        internal static PickupOutcome Healed(int amount)
        {
            return new PickupOutcome(PickupKind.Health, PickupRejection.None, null, false, 0, amount, 0, PickupSpec.NoOwner, null, false);
        }

        internal static PickupOutcome ArmorAdded(int amount)
        {
            return new PickupOutcome(PickupKind.Armor, PickupRejection.None, null, false, 0, 0, amount, PickupSpec.NoOwner, null, false);
        }

        internal static PickupOutcome TagTaken(int ownerId, Side? ownerSide, bool isOwnSide)
        {
            return new PickupOutcome(PickupKind.Tag, PickupRejection.None, null, false, 0, 0, 0, ownerId, ownerSide, isOwnSide);
        }
    }
}
