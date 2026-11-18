namespace Shooter.Server
{
    internal enum ServerDeployRefusal
    {
        None,
        CannotAct,
        UnknownType,
        Unusable,
        OutOfReach,
        LimitReached,
        NoGround,
        TooSteep,
        Uneven,
        Blocked,
    }
}
