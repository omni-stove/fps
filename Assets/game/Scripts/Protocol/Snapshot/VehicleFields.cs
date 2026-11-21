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
        Flares = 1 << 3,
        All = Transform | State | Parts | Flares,
    }
}
