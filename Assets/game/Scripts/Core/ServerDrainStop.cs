namespace Fps.Core
{
    /// <summary><see cref="ServerDrain"/> が止まると決めた理由。</summary>
    public enum ServerDrainStop
    {
        /// <summary>まだ止まると決めていない。</summary>
        None,

        /// <summary>つないでいる人がいない (いなくなった)。</summary>
        NoPlayers,

        /// <summary>今の試合が無いか、終わった。</summary>
        MatchEnded,

        /// <summary>待つ最長の秒数が過ぎた (0 秒で待たないときを含む)。</summary>
        TimedOut,

        /// <summary>準備中にもう一度終了を求められた。</summary>
        RequestedAgain,
    }
}
