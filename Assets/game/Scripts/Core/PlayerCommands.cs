using System;

namespace Shooter.Core
{
    /// <summary>移動と射撃のほかにプレイヤーが出す指示のボタンの組み合わせ。押されたものと押されているものを分けて持つ。</summary>
    [Flags]
    public enum PlayerCommands
    {
        None = 0,

        /// <summary>近接攻撃。</summary>
        Melee = 1 << 0,

        /// <summary>伏せ。</summary>
        Prone = 1 << 1,

        /// <summary>ピン (目印を付ける)。</summary>
        Ping = 1 << 2,

        /// <summary>1 つ目のアビリティ。</summary>
        Ability1 = 1 << 3,

        /// <summary>2 つ目のアビリティ。</summary>
        Ability2 = 1 << 4,

        /// <summary>分隊への指示。</summary>
        SquadCommand = 1 << 5,

        /// <summary>貯めたストリークの報酬を使う。</summary>
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
