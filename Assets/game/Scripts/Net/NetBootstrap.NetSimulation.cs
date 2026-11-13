namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、<c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c>
    /// (<see cref="Fps.Core.NetLaunchOptions.SimulatesNetwork"/>) で回線の劣化を真似る処理。
    /// </summary>
    /// <remarks>まだ何もしない。</remarks>
    public sealed partial class NetBootstrap
    {
        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void NetSimulationBeforeStart(ref string error);

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void NetSimulationAfterStart();

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void NetSimulationQuitRequested(ref bool allowQuit);
    }
}
