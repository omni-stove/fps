namespace Shooter.Server
{
    internal enum ServerDeployRefusal
    {
        None,
        RuleOff,
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
