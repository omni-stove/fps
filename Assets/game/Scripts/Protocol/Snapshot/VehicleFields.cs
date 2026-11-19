using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum VehicleFields
    {
        None = 0,
        Transform = 1 << 0,
        State = 1 << 1,
        Parts = 1 << 2,
        All = Transform | State | Parts,
    }
}
