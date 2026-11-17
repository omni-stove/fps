namespace Shooter.Core
{
    // 言葉を含まない識別なので、ログ・ネームプレート・キルフィードのどれでも同じ文字列にする。
    public static class KillFeedFormat
    {
        // オンラインでは名前を同期していないので Id で呼ぶ。
        public static string Player(int playerId)
        {
            return "P" + playerId;
        }
    }
}
