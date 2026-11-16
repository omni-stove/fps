namespace Fps.Core
{
    /// <summary>
    /// 弾倉が空になったときにリロードを始めるか。
    /// </summary>
    public enum ReloadMode
    {
        /// <summary>撃ち切ったら自動でリロードを始める。弾倉が空のまま引き金を引いたときも始める。</summary>
        Auto,

        /// <summary>Reload を押したときだけリロードする。弾倉が空で引き金を引いても空撃ちになるだけ。</summary>
        Manual,
    }
}
