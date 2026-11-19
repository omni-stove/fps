using System;

namespace Shooter.Protocol
{
    // 1 バイトに収まらなくなったので、SnapshotMessage は部分のビットを 2 バイトで書く。
    [Flags]
    public enum MatchFields
    {
        None = 0,
        Status = 1 << 0,
        Rules = 1 << 1,
        Objective = 1 << 2,
        Round = 1 << 3,
        ObjectiveState = 1 << 4,
        RoundResult = 1 << 5,
        ObjectiveProgress = 1 << 6,
        Payload = 1 << 7,
        BattleRoyale = 1 << 8,
        All = Status | Rules | Objective | Round | ObjectiveState | RoundResult | ObjectiveProgress | Payload | BattleRoyale,
    }
}
