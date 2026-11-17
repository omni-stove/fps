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

        /// <summary>左へ身を乗り出す。</summary>
        LeanLeft = 1 << 7,

        /// <summary>右へ身を乗り出す。</summary>
        LeanRight = 1 << 8,

        /// <summary>全員に向けたチャットを開く。</summary>
        Chat = 1 << 9,

        /// <summary>チームに向けたチャットを開く。</summary>
        TeamChat = 1 << 10,

        /// <summary>押している間だけ声を送る。</summary>
        PushToTalk = 1 << 11,
    }
}
