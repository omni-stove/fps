namespace Fps.Core
{
    /// <summary>
    /// リロード中に決まった間で押すと早く終わる仕組み (アクティブリロード) を使うか。
    /// </summary>
    public enum ActiveReload
    {
        /// <summary>リロードは常に同じ時間で終わる。</summary>
        Off,

        /// <summary>判定の幅の中で押せば早く終わり、外せば失敗の秒数だけ遅れる。</summary>
        On,
    }
}
