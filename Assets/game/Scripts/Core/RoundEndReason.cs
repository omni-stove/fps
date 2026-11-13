namespace Fps.Core
{
    /// <summary>爆破 (<see cref="SearchAndDestroy"/>) のラウンドが終わった理由。</summary>
    public enum RoundEndReason
    {
        /// <summary>設置した爆弾が起爆した (攻撃側の勝ち)。</summary>
        Detonated,

        /// <summary>防衛側が爆弾を解除した (防衛側の勝ち)。</summary>
        Defused,

        /// <summary>防衛側が全滅した (攻撃側の勝ち)。</summary>
        DefendersEliminated,

        /// <summary>設置前に攻撃側が全滅した (防衛側の勝ち)。</summary>
        AttackersEliminated,

        /// <summary>設置されないまま制限時間が切れた (防衛側の勝ち)。</summary>
        TimeUp,

        /// <summary>設置前に両陣営が同時に全滅した (引き分け)。</summary>
        BothEliminated,
    }
}
