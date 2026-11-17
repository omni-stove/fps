using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 参加コードを知れば非公開の試合に入れるので、ログにはコードを出さない。
    public static class JoinCodeLog
    {
        public const string Tag = "[Lobby]";

        public static string LoggingIn(string bucketId, int partySize)
        {
            return Tag + " join code: logging in to search " + Token(bucketId)
                + (partySize == 1 ? "" : " for a party of " + Count(partySize));
        }

        public static string LoggedIn(string userId)
        {
            return Tag + " join code: logged in with the device id user=" + Token(userId);
        }

        public static string Searched(int found, string bucketId, JoinCodeOutcome outcome)
        {
            return Tag + " join code: found " + Count(found) + " lobby(s) in " + Token(bucketId) + " outcome=" + outcome;
        }

        public static string Picked(string lobbyId, string endpoint, long players, int slots)
        {
            return Tag + " join code: picked id=" + Token(lobbyId) + " server=" + Token(endpoint)
                + " players=" + players.ToString(CultureInfo.InvariantCulture) + "/" + Count(slots);
        }

        public static string NotJoinedReason(JoinCodeOutcome outcome, string bucketId, string version, int partySize)
        {
            switch (outcome)
            {
                case JoinCodeOutcome.NotFound:
                    return "no server in " + Token(bucketId) + " has the join code";
                case JoinCodeOutcome.Full:
                    return partySize == 1
                        ? "the server with the join code is full"
                        : "the server with the join code has no room for a party of " + Count(partySize);
                case JoinCodeOutcome.VersionMismatch:
                    return "the server with the join code runs another version (this build is " + Token(version) + ")";
                default:
                    return "the server with the join code has no usable address or player count";
            }
        }

        public static string InvalidCode()
        {
            return "the join code is not 1-" + Count(JoinCode.MaxLength) + " characters of [0-9A-Za-z_-]";
        }

        public static string TimedOut(float seconds, bool loggingIn)
        {
            return "no answer from the lobby service after " + seconds.ToString(CultureInfo.InvariantCulture) + "s while "
                + (loggingIn ? "logging in" : "searching");
        }

        public static string Finished(JoinCodeOutcome outcome, string reason)
        {
            return Tag + " join code: " + outcome + " reason=" + Token(reason);
        }

        public static string Busy()
        {
            return "a join code search is already running";
        }

        public static string Unavailable(string reason)
        {
            return "the lobby service is not available: " + Token(reason);
        }

        public static string NotConnected(string reason)
        {
            return "found the server but cannot connect: " + Token(reason);
        }

        public static string Connecting(string endpoint)
        {
            return Tag + " join code: connecting to " + Token(endpoint);
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
