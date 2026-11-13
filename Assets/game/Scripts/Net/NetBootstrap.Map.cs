namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、<c>-map</c> (<see cref="Fps.Core.NetLaunchOptions.Map"/>) で選んだマップで試合を始める処理。
    /// </summary>
    /// <remarks>まだ何もしない。指定が無ければ今のシーン (NetTest) のまま。</remarks>
    public sealed partial class NetBootstrap
    {
        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void MapBeforeStart(ref string error);

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void MapAfterStart();

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void MapQuitRequested(ref bool allowQuit);
    }
}
