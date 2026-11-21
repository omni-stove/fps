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
        NoCharges,
        CoolingDown,
        OutOfReach,
        LimitReached,
        NoGround,
        TooSteep,
        Uneven,
        Blocked,

        // 起爆の要求 (DetonateRequestMessage) だけの理由
        NotDetonatable,
        NothingToDetonate,
    }
}
