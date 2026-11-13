namespace Fps.Core
{
    /// <summary>
    /// オンラインの試合の勝敗を何で決めるか。値の並びはアセットに乗るので、足すときは後ろに足す。
    /// </summary>
    public enum MatchObjective
    {
        /// <summary>拠点なし。チームデスマッチのキル数で決める。</summary>
        None,

        /// <summary>拠点を取り合い、拠点のポイントで決める。</summary>
        Domination,
    }
}
