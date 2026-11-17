using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum InputButtons : byte
    {
        None = 0,
        Sprint = 1,
        Jump = 2,
        Crouch = 4,
        Cover = 8,
        Aim = 16,
        Dodge = 32,
        Prone = 64,
        All = Sprint | Jump | Crouch | Cover | Aim | Dodge | Prone,
    }
}
