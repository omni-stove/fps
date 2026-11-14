namespace Fps.Core
{
    /// <summary>
    /// 近くを弾が飛ぶと制圧され、狙いや動きが鈍るか。
    /// </summary>
    public enum Suppression
    {
        /// <summary>制圧されない。</summary>
        Off,

        /// <summary>制圧される。</summary>
        On,
    }
}
