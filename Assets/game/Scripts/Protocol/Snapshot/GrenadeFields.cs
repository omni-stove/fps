using System;

namespace Shooter.Protocol
{
    /// <summary><see cref="GrenadeSnapshot"/> の部分。</summary>
    [Flags]
    public enum GrenadeFields
    {
        None = 0,
        /// <summary>投げた人とグレネードの Id と種類の Id。</summary>
        Identity = 1 << 0,
        /// <summary>位置と回転。</summary>
        Transform = 1 << 1,
        /// <summary>くっついているかと、くっついている相手。</summary>
        State = 1 << 2,
        All = Identity | Transform | State,
    }
}
