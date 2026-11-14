namespace Fps.Runtime
{
    /// <summary>HUD に分隊への指示の印 (<see cref="ISquadOrderView"/>) を付ける生成役。<see cref="SquadOrderUi.ViewFactory"/> に登録して使う。</summary>
    public interface ISquadOrderViewFactory
    {
        /// <summary>hud に指示の印の表示を (無ければ) 付けて返す。この HUD には出せなければ null。</summary>
        /// <param name="hud">シーンの HUD。null ではない。</param>
        ISquadOrderView Create(IHud hud);
    }
}
