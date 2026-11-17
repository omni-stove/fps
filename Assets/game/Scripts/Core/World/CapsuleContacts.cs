using System;

namespace Shooter.Core
{
    // 値は Unity の CollisionFlags と同じにそろえる。
    [Flags]
    public enum CapsuleContacts
    {
        None = 0,

        Sides = 1,

        Above = 2,

        Below = 4,
    }
}
