namespace Fps.Runtime
{
    /// <summary>HUD に印の表示 (<see cref="IPingView"/>) を付ける生成役。<see cref="PingUi.ViewFactory"/> に登録して使う。</summary>
    public interface IPingViewFactory
    {
        /// <summary>hud に印の表示を (無ければ) 付けて返す。この HUD には出せなければ null。</summary>
        /// <param name="hud">シーンの HUD。null ではない。</param>
        IPingView Create(IHud hud);
    }
}
