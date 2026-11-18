using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Server.Eos
{
    internal static class StatsIngestLog
    {
        public const string Tag = "[Stats]";

        public static string Off(string reason)
        {
            return Tag + " sending match stats to EOS is off: " + reason;
        }

        public static string On(int boards)
        {
            return Tag + " sending match stats to EOS boards=" + Text(boards);
        }

        public static string Queued(string matchId, int players, int skipped)
        {
            return Tag + " match " + matchId + " ended; queued stats for " + Text(players) + " player(s)"
                + (skipped > 0 ? ", skipped " + Text(skipped) + " without a verified EOS id" : string.Empty);
        }

        public static string NothingToSend(string matchId)
        {
            return Tag + " match " + matchId + " ended with no stats to send";
        }

        public static string Sent(string matchId, string userId, IReadOnlyList<StatIngest> stats)
        {
            return Tag + " sent match=" + matchId + " user=" + userId + " " + Describe(stats);
        }

        public static string Retrying(string matchId, string userId, int attempt, float delaySeconds, string error)
        {
            return Tag + " sending match=" + matchId + " user=" + userId + " failed (attempt " + Text(attempt) + "): " + error
                + "; retrying in " + delaySeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        public static string Dropped(string matchId, string userId, int attempts, string error)
        {
            return Tag + " dropped the stats of match=" + matchId + " user=" + userId + " after " + Text(attempts) + " attempt(s): " + error;
        }

        public static string Flushing(int pending, float seconds)
        {
            return Tag + " sending " + Text(pending) + " queued stat update(s) before quitting (up to "
                + seconds.ToString(CultureInfo.InvariantCulture) + "s)";
        }

        public static string FlushTimedOut(int pending)
        {
            return Tag + " " + Text(pending) + " stat update(s) were not sent before quitting";
        }

        public static string DuplicateStatName(string statName, string boardId)
        {
            return Tag + " board " + boardId + " uses stat " + statName + " that an earlier board already sends; it is sent once with the earlier board's value";
        }

        private static string Describe(IReadOnlyList<StatIngest> stats)
        {
            var parts = new string[stats.Count];
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = stats[i].ToString();
            }

            return string.Join(" ", parts);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
