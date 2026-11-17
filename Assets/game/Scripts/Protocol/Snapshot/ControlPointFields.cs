using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum ControlPointFields
    {
        None = 0,
        State = 1 << 0,
        All = State,
    }
}
