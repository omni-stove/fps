namespace Fps.Core
{
    /// <summary>メインメニューのボタンで選ぶ行き先。オフラインの行き先でどのシーンをどう読むかは <see cref="MenuRoute.Plan(MenuDestination, string)"/> が決める。</summary>
    public enum MenuDestination
    {
        /// <summary>キャンペーン (Campaign01)。</summary>
        Campaign,

        /// <summary>オンライン: 専用サーバーを探してつなぐ (<c>-lobby quickmatch</c> と同じ)。始めるのは MenuNavigator.OnlineStarter。</summary>
        QuickMatch,

        /// <summary>射撃場 (Bootstrap)。</summary>
        Range,

        /// <summary>射撃場 (Bootstrap) で、終えたことがあってもチュートリアルを最初から出す。</summary>
        Tutorial,

        /// <summary>ゲームを終える。</summary>
        Quit,
    }
}
