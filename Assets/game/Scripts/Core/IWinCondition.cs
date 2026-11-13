namespace Fps.Core
{
    /// <summary>
    /// 試合の終わり方。<see cref="ModeSession"/> は渡された順に調べ、最初に決着を返した条件で終える。
    /// </summary>
    public interface IWinCondition
    {
        /// <summary>
        /// 得点の上限。どれかの陣営の得点がこれに届いたら、その陣営の勝ちで終える。
        /// 時間で入る得点は届いた時刻で止めて上限ちょうどにする。上限を持たない条件は null。
        /// </summary>
        double? ScoreLimit { get; }

        /// <summary>
        /// <see cref="ModeSession.Tick"/> の頭、時間を進める前に、前の Tick からの死亡と退出をまとめて調べる
        /// (倒れた瞬間に調べないのは、同じ間の相打ちを報告の順番によらず同時のものとして扱うため)。
        /// </summary>
        ModeOutcome CheckStanding(ModeSession session);

        /// <summary>残り時間が 0 になったときの勝敗。どの条件も決めなければ引き分けで終える。</summary>
        ModeOutcome CheckTimeUp(ModeSession session);
    }
}
