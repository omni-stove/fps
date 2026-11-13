namespace Fps.Core
{
    /// <summary>
    /// モードが陣営の得点を足す口。キルで入る点・時間で入る点 (拠点を持っている間など) を <see cref="ModeSession"/> に返す。
    /// 1 回きりの目標の点 (爆破・確認など) は、モードが <see cref="ModeSession.AddScore"/> で直接足す。
    /// </summary>
    public interface IScoreRule
    {
        /// <summary>
        /// killerId が別の陣営の victimId を倒したときに、killerId の陣営へ足す点。0 以上の有限値。
        /// 味方や自分を倒したときは呼ばれない。
        /// </summary>
        double PointsForKill(ModeSession session, int killerId, int victimId);

        /// <summary>
        /// side に 1 秒あたり入る点。0 以上の有限値。<see cref="ModeSession.Tick"/> の頭で陣営ごとに 1 度ずつ呼び、
        /// その Tick の間は変わらないものとして足す (Tick の途中で目標を取っても、点はその次の Tick から入る)。
        /// </summary>
        double PointsPerSecond(ModeSession session, Side side);

        /// <summary>
        /// 目標 (拠点など) を seconds 秒進める。<see cref="PointsPerSecond"/> を集めた後、点を足す前に呼ぶ。
        /// 得点上限に届く時刻が Tick の途中にあれば、seconds はそこまでの秒数になる。
        /// </summary>
        void Advance(ModeSession session, float seconds);
    }
}
