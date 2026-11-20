namespace Shooter.Server
{
    internal enum ServerDeployRefusal
    {
        None,
        RuleOff,
        CannotAct,
        UnknownType,
        Unusable,
        NoCharges,
        CoolingDown,
        OutOfReach,
        LimitReached,
        NoGround,
        TooSteep,
        Uneven,
        Blocked,
    }
}
