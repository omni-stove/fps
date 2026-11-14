namespace Fps.Core
{
    /// <summary><see cref="PingBoard.Track"/> で印がどうなったか。</summary>
    public enum PingTrackResult
    {
        /// <summary>変わらない (同じ位置に見えている、既に見失っている、場所の印)。</summary>
        Unchanged,

        /// <summary>見えている敵を追って位置が変わった。</summary>
        Moved,

        /// <summary>この呼び出しで敵を見失った。印は最後に見た位置に残る。</summary>
        Lost,

        /// <summary>敵が倒れたか居なくなったので印を消した。</summary>
        Gone,

        /// <summary>その番号の印は無い (消えた後など)。</summary>
        Missing,
    }
}
