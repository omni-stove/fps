using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum InputButtons : ushort
    {
        None = 0,
        Sprint = 1,
        Jump = 2,
        Crouch = 4,
        Cover = 8,
        Aim = 16,
        Dodge = 32,
        Prone = 64,
        LeanLeft = 128,
        LeanRight = 256,
        Melee = 512,
        OpenParachute = 1024,
        CloseParachute = 2048,
        Slide = 4096,
        Grapple = 8192,
        Takedown = 16384,
        All = Sprint | Jump | Crouch | Cover | Aim | Dodge | Prone | LeanLeft | LeanRight | Melee | OpenParachute | CloseParachute
            | Slide | Grapple | Takedown,
    }
}
