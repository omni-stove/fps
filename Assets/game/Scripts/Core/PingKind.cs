namespace Fps.Core
{
    /// <summary>印 (<see cref="PingMarker"/>) を何に付けたか。</summary>
    public enum PingKind
    {
        /// <summary>場所。付けた位置から動かない。</summary>
        Location,

        /// <summary>敵。見えている間は敵を追い、見失うと最後の位置に残る。</summary>
        Enemy,
    }
}
