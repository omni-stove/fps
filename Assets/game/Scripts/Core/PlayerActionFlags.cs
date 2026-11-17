using System;

namespace Shooter.Core
{
    [Flags]
    public enum PlayerActionConditions
    {
        None = 0,

        Sprinting = 1 << 0,

        Crouching = 1 << 1,

        Dodging = 1 << 2,

        Vaulting = 1 << 3,

        Grounded = 1 << 4,

        InCover = 1 << 5,

        BlindFiring = 1 << 6,

        Aiming = 1 << 7,

        Firing = 1 << 8,

        Reloading = 1 << 9,

        Downed = 1 << 10,

        Meleeing = 1 << 11,

        Staggered = 1 << 12,

        Sliding = 1 << 13,

        Mantling = 1 << 14,

        Prone = 1 << 15,

        OnLadder = 1 << 16,

        Ziplining = 1 << 17,

        Parachuting = 1 << 18,

        InVehicle = 1 << 19,

        AbilityMoving = 1 << 20,
    }
}
