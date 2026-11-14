using System;

namespace Fps.Core
{
    /// <summary><see cref="CapsuleMover.Move"/> でカプセルが触れた向き。値は Unity の CollisionFlags と同じ。</summary>
    [Flags]
    public enum CapsuleContacts
    {
        None = 0,

        /// <summary>壁や、歩いて上がれない坂に触れた。</summary>
        Sides = 1,

        /// <summary>上へ動いて天井に触れた。</summary>
        Above = 2,

        /// <summary>下へ動いて歩ける面に立った。</summary>
        Below = 4,
    }
}
