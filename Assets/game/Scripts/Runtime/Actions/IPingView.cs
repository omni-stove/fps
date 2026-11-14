namespace Fps.Runtime
{
    /// <summary>
    /// 印 (<see cref="PingController.Markers"/>) を画面に出す表示。画面の外の印は端に寄せて向きを出す。
    /// 既定は UI のアセンブリの PingMarkersView で、<see cref="PingUi.ViewFactory"/> を差し替えると別の表示にできる。
    /// </summary>
    public interface IPingView
    {
        /// <summary>source の印を出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        void Show(PingController source);
    }
}
