namespace Fps.Core
{
    /// <summary><see cref="CoverState.Step"/> 1 回で起きた、ログに出す状態の変化。</summary>
    public enum CoverTransition
    {
        /// <summary>ログに出す変化は無い (入りかけから中へ移った場合を含む)。</summary>
        None,

        /// <summary>カバーに入った。</summary>
        Enter,

        /// <summary>覗き始めた。</summary>
        Peek,

        /// <summary>覗くのをやめて隠れた。</summary>
        Unpeek,

        /// <summary>カバーから出た。</summary>
        Exit,
    }
}
