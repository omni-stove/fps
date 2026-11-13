namespace Fps.Core
{
    /// <summary><see cref="MenuTransition"/> がすること。</summary>
    public enum MenuTransitionKind
    {
        /// <summary><see cref="MenuTransition.ScenePath"/> のシーンを読む。</summary>
        LoadScene,

        /// <summary>アプリケーションを終える。</summary>
        Quit,
    }
}
