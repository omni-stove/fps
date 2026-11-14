using System;

namespace Fps.Protocol
{
    /// <summary><see cref="GrenadeSnapshot"/> の部分。</summary>
    [Flags]
    public enum GrenadeFields
    {
        None = 0,
        /// <summary>投げた人とグレネードの Id。</summary>
        Identity = 1 << 0,
        /// <summary>位置と回転。</summary>
        Transform = 1 << 1,
        All = Identity | Transform,
    }
}
