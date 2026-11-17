namespace Shooter.Core
{
    public enum PickupRejection
    {
        None = 0,

        CollectorDown = 1,

        SideNotAllowed = 2,

        CannotHoldWeapons = 3,

        NoWeapon = 4,

        AmmoFull = 5,

        HealthFull = 6,

        NoArmor = 7,

        ArmorFull = 8,

        Gone = 9,

        ApplyFailed = 10,
        NoInventory = 11,
        InventoryFull = 12,
    }
}
