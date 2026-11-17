namespace Shooter.Core
{
    /// <summary>旗取り (<see cref="CaptureTheFlag"/>) の旗の状態。</summary>
    public enum FlagState
    {
        /// <summary>自陣の台にある。敵が触れると持っていける。</summary>
        AtBase,

        /// <summary>敵の誰かが運んでいる。</summary>
        Carried,

        /// <summary>運ぶ人が倒れるか抜けて、その場に落ちている。敵が触れると持ち、味方が触れるか秒数がたつと台に戻る。</summary>
        Dropped,
    }
}
