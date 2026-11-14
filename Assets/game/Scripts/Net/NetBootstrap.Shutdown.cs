namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、サーバーが終了を求められたとき (SIGTERM など) に新しい参加を断り、
    /// 今の試合が終わるまで (最長 <c>-drain-timeout</c> 秒、<see cref="Fps.Core.NetLaunchOptions.DrainTimeoutSeconds"/>) 止まるのを待つ処理。
    /// </summary>
    /// <remarks>まだ何もしない。今は求められたらすぐ止まる。</remarks>
    public sealed partial class NetBootstrap
    {
        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void ShutdownBeforeStart(ref string error);

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void ShutdownAfterStart();

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void ShutdownQuitRequested(ref bool allowQuit);
    }
}
