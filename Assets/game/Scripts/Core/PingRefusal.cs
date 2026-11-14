namespace Fps.Core
{
    /// <summary>印を付けようとして付けなかった理由。</summary>
    public enum PingRefusal
    {
        /// <summary>付けた。</summary>
        None,

        /// <summary>ルールの <see cref="ActionRules.Ping"/> が Off (まだルールを受け取っていないときも)。</summary>
        RuleOff,

        /// <summary>付ける人が倒れている。</summary>
        Dead,

        /// <summary>照準を決める視点が無い。</summary>
        NoView,

        /// <summary>照準の先に届く距離のものが無い。</summary>
        NoTarget,

        TooSoon,

        InvalidAim,

        Remote,
    }
}
