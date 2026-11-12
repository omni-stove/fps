namespace Fps.Core
{
    /// <summary><see cref="Match"/> の勝敗。</summary>
    public enum MatchResult
    {
        /// <summary>規定キル数に到達して勝利。</summary>
        Won,

        /// <summary>時間切れかプレイヤー死亡で敗北。</summary>
        Lost,
    }
}
