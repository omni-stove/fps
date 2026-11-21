namespace Shooter.Server
{
    // 拾う要求 (PickupRequestMessage) と触れて拾うのを断った理由。拾う人の側の理由 (体力が満タンなど) は Judged にまとめ、
    // 中身の判定 (PickupJudge) の PickupRejection をログに添える
    internal enum ServerPickupRefusal
    {
        None,

        UnknownItem,

        CannotAct,

        Gone,

        OutOfReach,

        OutOfSight,

        CoolingDown,

        NothingToRefill,

        Judged,

        ApplyFailed,
    }
}
