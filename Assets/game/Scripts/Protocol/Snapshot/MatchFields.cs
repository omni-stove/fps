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
        All = Status | Rules | Objective,
    }
}
