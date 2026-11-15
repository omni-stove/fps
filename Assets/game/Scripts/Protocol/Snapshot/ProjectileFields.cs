using System;

namespace Fps.Protocol
{
    [Flags]
    public enum ProjectileFields
    {
        None = 0,
        Identity = 1 << 0,
        Motion = 1 << 1,
        All = Identity | Motion,
    }
}
