namespace Fps.Core
{
    /// <summary><see cref="ExecutionSession.Interact"/> で、ダウンした人へのインタラクトの長押しを進めた結果。</summary>
    public enum ExecutionStep
    {
        /// <summary>
        /// 進めなかった (ラウンド中でない・自分自身・押した人が倒れているかダウン中・相手がダウンしていない・
        /// この Tick で別の人がすでに同じ相手に押している)。
        /// </summary>
        Rejected,

        /// <summary>味方を起こす累計を進めたが、まだ起き上がっていない。</summary>
        Reviving,

        /// <summary>味方が起き上がった。</summary>
        Revived,

        /// <summary>敵へのとどめの累計を進めたが、まだ倒していない。</summary>
        Executing,

        /// <summary>敵にとどめを刺して倒した。</summary>
        Executed,
    }
}
