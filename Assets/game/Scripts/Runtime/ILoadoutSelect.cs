namespace Fps.Runtime
{
    /// <summary>ロードアウトを選ぶ画面の開閉の様子。ゲーム進行は <see cref="GameUi.IsLoadoutSelectOpen"/> から読む。</summary>
    public interface ILoadoutSelect
    {
        /// <summary>カーソルを解放して開いているロードアウトの画面があるか。</summary>
        bool IsAnyOpen { get; }
    }
}
