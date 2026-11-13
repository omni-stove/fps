namespace Fps.Core
{
    /// <summary><see cref="ExecutionSession.ReportLethal"/> で、体力が 0 になった人をどう扱ったか。</summary>
    public enum ExecutionHit
    {
        /// <summary>ラウンド中でない・すでにダウンしているか倒れているので、何もしなかった。</summary>
        Ignored,

        /// <summary>ダウンさせた。とどめか出血で倒れるまで、ラウンドからは外れない。</summary>
        Downed,

        /// <summary>ダウンを使わないルールなので、その場で倒した。</summary>
        Killed,
    }
}
