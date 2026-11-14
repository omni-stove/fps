namespace Fps.Core
{
    /// <summary><see cref="BattleRoyaleSession.ReportLethal"/> で、体力が 0 になった人をどう扱ったか。</summary>
    public enum BattleRoyaleHit
    {
        /// <summary>試合中でない・すでにダウンしているか倒れているので、何もしなかった。</summary>
        Ignored,

        /// <summary>ダウンさせた。分隊の味方が起こすか、出血で倒れるまで試合からは外れない。</summary>
        Downed,

        /// <summary>
        /// その場で倒した。ダウンを使わないルールか、分隊にほかに立っている人がいなかった (分隊の全滅) とき。
        /// 全滅のときは、ダウン中だった分隊の味方もいっしょに倒している。
        /// </summary>
        Killed,
    }
}
