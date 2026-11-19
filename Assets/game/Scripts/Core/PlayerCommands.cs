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

        LeanLeft = 1 << 7,

        LeanRight = 1 << 8,

        Chat = 1 << 9,

        TeamChat = 1 << 10,

        PushToTalk = 1 << 11,

        SwitchStreak = 1 << 12,
    }
}
