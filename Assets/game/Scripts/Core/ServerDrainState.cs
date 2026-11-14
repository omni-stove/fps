namespace Fps.Core
{
    /// <summary><see cref="ServerDrain"/> の段階。</summary>
    public enum ServerDrainState
    {
        /// <summary>まだ終了を求められていない。</summary>
        Idle,

        /// <summary>止まる準備中。新しい参加を断り、試合の終わりを待つ。</summary>
        Draining,

        /// <summary>止まると決めた。以後、変わらない。</summary>
        Finished,
    }
}
