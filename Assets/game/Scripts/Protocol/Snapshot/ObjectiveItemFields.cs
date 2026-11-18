using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum ObjectiveItemFields
    {
        None = 0,
        Identity = 1 << 0,
        Position = 1 << 1,
        State = 1 << 2,
        All = Identity | Position | State,
    }
}
