namespace Shooter.Core
{
    public enum VehicleLeaveReason
    {
        Request = 0,

        Wrecked = 1,

        RiderDied = 2,

        Disabled = 3,

        Forced = 4,

        // 乗っている人が消えたので置き直す相手がおらず、座席を空けるだけにする。
        RiderRemoved = 5,

        BailOut = 6,
    }
}
