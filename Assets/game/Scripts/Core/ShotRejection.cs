namespace Fps.Core
{
    /// <summary>サーバーが射撃を捨てる理由。<see cref="ShotValidator.Check"/> の結果に、連射の制限 (<see cref="ShotBudget"/>) を加えたもの。</summary>
    public enum ShotRejection
    {
        /// <summary>受け付ける。</summary>
        None,

        /// <summary>始点に NaN か無限大が入っている。</summary>
        InvalidOrigin,

        /// <summary>向きが 0 か、NaN / 無限大が入っている。</summary>
        InvalidDirection,

        /// <summary>時点の tick が負か、割合が 0 以上 1 未満でない。</summary>
        InvalidTime,

        /// <summary>始点が、サーバーの知る撃った本人の位置から離れすぎている。</summary>
        OriginTooFar,

        /// <summary>撃った本人の目の位置から始点までの間に遮蔽物がある (Net 側で判定する)。</summary>
        OriginBlocked,

        /// <summary>連射の間隔より速く撃っている (<see cref="ShotBudget.TryConsume"/> が false)。</summary>
        FireRateExceeded,
    }
}
