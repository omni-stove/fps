namespace Shooter.Server
{
    internal enum ServerDeployRefusal
    {
        None,
        RuleOff,
        CannotAct,
        UnknownType,
        Unusable,
        NotEquipped,
        OutOfReach,
        LimitReached,
        NoGround,
        TooSteep,
        Uneven,
        Blocked,
    }
}
