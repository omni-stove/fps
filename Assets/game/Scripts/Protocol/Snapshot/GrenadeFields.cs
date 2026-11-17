using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum GrenadeFields
    {
        None = 0,
        Identity = 1 << 0,
        Transform = 1 << 1,
        State = 1 << 2,
        All = Identity | Transform | State,
    }
}
