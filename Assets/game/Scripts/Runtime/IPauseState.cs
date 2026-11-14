namespace Fps.Runtime
{
    /// <summary>ポーズ画面の開閉の様子。ゲーム進行は <see cref="GameUi.IsPauseOpen"/> から読む。</summary>
    public interface IPauseState
    {
        /// <summary>どれかのポーズ画面が開いているか。</summary>
        bool IsAnyOpen { get; }
    }
}
