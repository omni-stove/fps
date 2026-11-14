namespace Fps.Runtime
{
    /// <summary>
    /// 分隊への指示の印 (<see cref="SquadCommandController.CurrentOrder"/>) を画面に出す表示。
    /// 既定は UI のアセンブリの SquadOrderMarkerView で、<see cref="SquadOrderUi.ViewFactory"/> を差し替えると別の表示にできる。
    /// </summary>
    public interface ISquadOrderView
    {
        /// <summary>source の指示を出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        void Show(SquadCommandController source);
    }
}
