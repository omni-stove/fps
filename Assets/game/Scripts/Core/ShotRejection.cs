namespace Shooter.Core
{
    public enum ShotRejection
    {
        None,

        InvalidOrigin,

        InvalidDirection,

        InvalidTime,

        OriginTooFar,

        OriginBlocked,

        FireRateExceeded,

        NoWeapon,

        Reloading,

        OutOfAmmo,

        UnknownWeapon,

        WeaponMismatch,

        Switching,

        BlindFireBlocked,

        Dodging,

        Vaulting,

        NotAiming,

        Overheated,

        Undercharged,

        SeatCannotFire,

        OutsideSeatArc,
    }
}
