using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class GuardianRulesLog
    {
        public const string Tag = "[Guardian]";

        public const string Killed = "killed";

        public const string Left = "left";

        public static string Started(float respawnSeconds, float timeLimitSeconds)
        {
            return Tag + " net respawn=" + Text(respawnSeconds) + " timeLimit=" + Text(timeLimitSeconds);
        }

        public static string LeaderChosen(Team team, int playerId)
        {
            return Tag + " leader team=" + team + " player=" + Text(playerId);
        }

        public static string LeaderFell(Team team, int playerId, string reason)
        {
            return Tag + " leader-down team=" + team + " player=" + Text(playerId) + " reason=" + reason + " respawn=off";
        }

        public static string Eliminated(Team? winner)
        {
            return Tag + " eliminated winner=" + (winner.HasValue ? winner.Value.ToString() : MatchLog.Draw);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
