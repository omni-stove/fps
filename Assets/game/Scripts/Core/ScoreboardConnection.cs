namespace Fps.Core
{
    /// <summary>
    /// スコアボードの 1 行に出す接続状態。
    /// </summary>
    public enum ScoreboardConnection
    {
        /// <summary>ネットワークを通さない (オフラインの自分と標的)。接続状態の欄は空にする。</summary>
        None,

        /// <summary>サーバーにつながっている。</summary>
        Connected,

        /// <summary>切断したが成績はまだ残っている。</summary>
        Disconnected,
    }
}
