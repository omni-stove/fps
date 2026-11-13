namespace Fps.Core
{
    /// <summary>Ping の良し悪し。HUD の回線の表示の色を決める (<see cref="NetStatsFormat.PingLevel"/>)。</summary>
    public enum NetPingLevel
    {
        /// <summary><see cref="NetStatsFormat.WarningPingMs"/> 未満か、まだ分からない。</summary>
        Good,

        /// <summary><see cref="NetStatsFormat.WarningPingMs"/> 以上 <see cref="NetStatsFormat.BadPingMs"/> 未満。黄色で出す。</summary>
        Warning,

        /// <summary><see cref="NetStatsFormat.BadPingMs"/> 以上。赤で出す。</summary>
        Bad,
    }
}
