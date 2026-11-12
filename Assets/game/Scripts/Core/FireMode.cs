namespace Fps.Core
{
    /// <summary>
    /// 射撃モード。トリガーを押し続けたときの振る舞いを表す。
    /// </summary>
    public enum FireMode
    {
        /// <summary>押し続けている間、連射間隔ごとに撃ち続ける。</summary>
        Auto,

        /// <summary>1 回押すごとに 1 発だけ撃つ。</summary>
        Semi,
    }
}
