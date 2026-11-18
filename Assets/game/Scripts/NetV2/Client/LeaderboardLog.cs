using System.Globalization;

namespace Shooter.NetV2
{
    public static class LeaderboardLog
    {
        public const string Tag = "[Leaderboard]";

        public static string Off(string reason)
        {
            return Tag + " leaderboards are off: " + reason;
        }

        public static string UnknownBoard(string boardId)
        {
            return Tag + " no leaderboard has the id '" + boardId + "' in the leaderboard tuning";
        }

        public static string LoggingIn()
        {
            return Tag + " logging in";
        }

        public static string LoggedIn(string userId)
        {
            return Tag + " logged in user=" + userId;
        }

        public static string LoginFailed(string error)
        {
            return Tag + " logging in failed: " + error;
        }

        public static string Querying(string boardId)
        {
            return Tag + " querying board=" + boardId;
        }

        public static string Loaded(string boardId, int rows, int? localRank)
        {
            return Tag + " loaded board=" + boardId + " rows=" + rows.ToString(CultureInfo.InvariantCulture)
                + " localRank=" + (localRank.HasValue ? localRank.Value.ToString(CultureInfo.InvariantCulture) : "-");
        }

        public static string Failed(string boardId, string error)
        {
            return Tag + " querying board=" + boardId + " failed: " + error;
        }
    }
}
