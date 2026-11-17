namespace Shooter.Core
{
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
            bool isOwnSideTag,
            string droppedWeaponId = null,
            string itemId = null,
            int items = 0)
        {
            ItemId = itemId;
            Items = items;
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
            DroppedWeaponId = droppedWeaponId;
        }

        public PickupKind Kind { get; }

        public PickupRejection Rejection { get; }

        public bool IsCollected => Rejection == PickupRejection.None;

        public string WeaponId { get; }

        public bool AddsWeapon { get; }

        public string DroppedWeaponId { get; }

        public int ReserveAmmo { get; }

        public int Health { get; }

        public int Armor { get; }

        public int TagOwnerId { get; }

        public Side? TagOwnerSide { get; }

        public bool IsOwnSideTag { get; }

        public string ItemId { get; }

        public int Items { get; }

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

        internal static PickupOutcome WeaponSwapped(string weaponId, string droppedWeaponId)
        {
            return new PickupOutcome(PickupKind.Weapon, PickupRejection.None, weaponId, true, 0, 0, 0, PickupSpec.NoOwner, null, false, droppedWeaponId);
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

        internal static PickupOutcome ItemsAdded(string itemId, int count)
        {
            return new PickupOutcome(PickupKind.Item, PickupRejection.None, null, false, 0, 0, 0, PickupSpec.NoOwner, null, false, null, itemId, count);
        }

        internal static PickupOutcome TagTaken(int ownerId, Side? ownerSide, bool isOwnSide)
        {
            return new PickupOutcome(PickupKind.Tag, PickupRejection.None, null, false, 0, 0, 0, ownerId, ownerSide, isOwnSide);
        }
    }
}
