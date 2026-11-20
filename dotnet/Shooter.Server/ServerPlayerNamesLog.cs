using System.Globalization;

namespace Shooter.Server
{
    // 名簿に載る名前は PlayerDisplayName.Sanitize で制御文字を外した後のものなので、そのまま行に書いても行を偽れない。
    // 前後の空白や区切りの文字が紛れないよう、名前は引用符で囲む
    internal static class ServerPlayerNamesLog
    {
        public const string Tag = "[Names]";

        public static string Joined(int playerId, string name, int players)
        {
            return Tag + " player=" + Text(playerId) + " name=\"" + name + "\" roster players=" + Text(players);
        }

        public static string Left(int playerId, int players)
        {
            return Tag + " player=" + Text(playerId) + " left roster players=" + Text(players);
        }

        public static string Full(int playerId, int players)
        {
            return Tag + " player=" + Text(playerId) + " is not in the roster because it already holds " + Text(players)
                + " players; clients show the id name";
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
