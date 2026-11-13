namespace Fps.Core
{
    /// <summary><see cref="IdleTracker"/> の 1 回の操作で、そのプレイヤーの放置の状態がどう変わったか。</summary>
    public enum IdleChange
    {
        /// <summary>知らせることは無い。</summary>
        None,

        /// <summary>残りが <see cref="IdleTracker.WarningSeconds"/> 以下で数えている状態になった。警告を出す。</summary>
        Warned,

        /// <summary>出していた警告が要らなくなった (入力があった・数えるのをやめた)。警告を消す。</summary>
        WarningCleared,

        /// <summary>残りが 0 になった。サーバーから切る。プレイヤーごとに 1 度だけ返す。</summary>
        Kicked,
    }
}
