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

        // 盾の武器は弾を撃たない (オフラインの WeaponController.IsFireBlocked と同じ)。
        ShieldWeapon,
    }
}
