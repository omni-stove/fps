namespace Fps.Core
{
    /// <summary>メインメニューのボタンで選ぶ行き先。どのシーンをどう読むかは <see cref="MenuRoute.Plan(MenuDestination, string)"/> が決める。</summary>
    public enum MenuDestination
    {
        /// <summary>キャンペーン (Campaign01)。</summary>
        Campaign,

        /// <summary>オンライン: ロビーを作って Host になる (<c>-lobby create</c> と同じ)。</summary>
        CreateLobby,

        /// <summary>オンライン: ロビーを探して入る (<c>-lobby join</c> と同じ)。</summary>
        JoinLobby,

        /// <summary>オンライン: 専用サーバーを探して UDP でつなぐ (<c>-lobby quickmatch</c> と同じ)。</summary>
        QuickMatch,

        /// <summary>射撃場 (Bootstrap)。</summary>
        Range,

        /// <summary>射撃場 (Bootstrap) で、終えたことがあってもチュートリアルを最初から出す。</summary>
        Tutorial,

        /// <summary>ゲームを終える。</summary>
        Quit,
    }
}
