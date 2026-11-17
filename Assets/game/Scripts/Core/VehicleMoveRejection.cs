namespace Shooter.Core
{
    public enum VehicleMoveRejection
    {
        None = 0,

        NotDriver = 1,

        Wrecked = 2,

        StaleCorrection = 3,

        TooFast = 4,

        Blocked = 5,

        OutOfOrder = 6,
    }
}
