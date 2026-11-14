namespace Fps.Core
{
    /// <summary>サーバーのログの形式。コマンドライン引数 <c>-log-format text|json</c> に対応する。</summary>
    public enum ServerLogFormat
    {
        /// <summary>今までどおり <c>[Net]</c> / <c>[Match]</c> 付きの文だけを出す。</summary>
        Text,

        /// <summary>文に加えて、試合のイベントを 1 行の JSON (<see cref="ServerEventLog"/>) で標準出力に出す。</summary>
        Json,
    }
}
