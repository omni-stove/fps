using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum DeployableFields
    {
        None = 0,
        Identity = 1 << 0,
        Transform = 1 << 1,
        Health = 1 << 2,
        All = Identity | Transform | Health,
    }
}
