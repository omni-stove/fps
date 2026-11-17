using System.Globalization;

namespace Shooter.Server
{
    internal static class ServerVoiceLog
    {
        public const string Tag = "[Voice]";

        public static string Roster(int players)
        {
            return Tag + " roster players=" + players.ToString(CultureInfo.InvariantCulture);
        }

        public static string Ambiguous(string productUserId)
        {
            return Tag + " product user id " + productUserId + " is claimed by more than one player; no one is heard as its owner";
        }
    }
}
