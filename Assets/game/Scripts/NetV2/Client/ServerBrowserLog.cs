using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ServerBrowserLog
    {
        public const string Tag = "[Lobby]";

        public static string LoggingIn(string bucketId)
        {
            return Tag + " browser: logging in to list " + Token(bucketId);
        }

        public static string LoggedIn(string userId)
        {
            return Tag + " browser: logged in with the device id user=" + Token(userId);
        }

        public static string Searching(string bucketId)
        {
            return Tag + " browser: searching " + Token(bucketId);
        }

        public static string Searched(int found, int listed, string bucketId)
        {
            return Tag + " browser: found " + Count(found) + " lobby(s) in " + Token(bucketId) + ", listing " + Count(listed);
        }

        public static string Picked(ServerListing listing)
        {
            return Tag + " browser: picked " + listing;
        }

        public static string Failed(string reason)
        {
            return Tag + " browser: unavailable reason=" + Token(reason);
        }

        public static string NotJoined(string serverId, ServerJoinOutcome outcome, string reason)
        {
            return Tag + " browser: not joined id=" + Token(serverId) + " outcome=" + outcome + " reason=" + Token(reason);
        }

        public static string TimedOut(float seconds, bool loggingIn)
        {
            return "no answer from the lobby service after " + seconds.ToString(CultureInfo.InvariantCulture) + "s while "
                + (loggingIn ? "logging in" : "searching");
        }

        public static string Busy()
        {
            return "a server is already being joined";
        }

        public static string NotLoaded()
        {
            return "the server list has not been loaded";
        }

        public static string NotListed()
        {
            return "the server is no longer in the list; refresh it";
        }

        public static string NoRoom(int free, int partySize)
        {
            return partySize == 1
                ? "the server is full"
                : "the server has room for " + Count(free) + ", not a party of " + Count(partySize);
        }

        public static string Unavailable(string reason)
        {
            return "the lobby service is not available: " + Token(reason);
        }

        public static string NotConnected(string reason)
        {
            return "picked the server but cannot connect: " + Token(reason);
        }

        public static string CannotPing(string address, string reason)
        {
            return Tag + " browser: cannot ping " + Token(address) + " reason=" + Token(reason);
        }

        public static string Connecting(string endpoint)
        {
            return Tag + " browser: connecting to " + Token(endpoint);
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
