using System;

namespace Shooter.Core
{
    [Flags]
    public enum PlayerCommands
    {
        None = 0,

        Melee = 1 << 0,

        Prone = 1 << 1,

        Ping = 1 << 2,

        Ability1 = 1 << 3,

        Ability2 = 1 << 4,

        SquadCommand = 1 << 5,

        UseStreak = 1 << 6,
    }
}
