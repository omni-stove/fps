namespace Shooter.Core
{
    /// <summary>
    /// プレイヤーを Id で呼ぶときの名前。言葉を含まない識別なので、ログ・ネームプレート・キルフィードのどれでも同じ文字列にする。
    /// </summary>
    public static class KillFeedFormat
    {
        /// <summary>オンラインのプレイヤーの名前 (<c>P&lt;id&gt;</c>)。名前を同期していないので Id で呼ぶ。</summary>
        public static string Player(int playerId)
        {
            return "P" + playerId;
        }
    }
}
