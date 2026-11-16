namespace Fps.Core
{
    /// <summary>
    /// ふつうのジャンプで跳べるか。Off でも Jump の入力は、乗り越え・崖のぼり・伏せから立つ・はしごやジップラインから離れる・パラシュートを開くには使う。
    /// </summary>
    public enum Jump
    {
        /// <summary>跳べない。前に乗り越えられる物もよじ登れる段も無ければ、Jump を押しても何もしない。</summary>
        Off,

        /// <summary>跳べる。</summary>
        On,
    }
}
