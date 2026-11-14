using System;

namespace Fps.Core
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
    }
}
