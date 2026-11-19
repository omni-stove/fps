using System;

namespace Shooter.Protocol
{
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
        All = Status | Rules | Objective | Round | ObjectiveState | RoundResult | ObjectiveProgress,
    }
}
