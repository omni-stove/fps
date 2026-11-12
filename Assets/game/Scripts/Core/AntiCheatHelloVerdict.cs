namespace Fps.Core
{
    /// <summary>相手の Hello を受け取ったときにすること (<see cref="AntiCheatPeers.ReceiveHello"/>)。</summary>
    public enum AntiCheatHelloVerdict
    {
        /// <summary>接続中の相手ではない。捨てる。</summary>
        Unknown = 0,

        /// <summary>前にも受け取った。相手がこちらの Hello を待っているかもしれないので、こちらの Hello を送り直す。</summary>
        Duplicate = 1,

        /// <summary>両方とも EAC が無効。EAC 抜きでそのまま遊ぶ。</summary>
        Accepted = 2,

        /// <summary>両方とも EAC が有効。返したハンドルで EAC にピアとして登録する。</summary>
        Registered = 3,

        /// <summary>片方だけ EAC が有効。相手を切断する。</summary>
        Rejected = 4,
    }
}
