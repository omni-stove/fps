using System.Globalization;

namespace Shooter.NetV2
{
    public static class MatchLobbyLog
    {
        public const string Tag = "[Lobby]";

        public static string Joining(string lobbyId)
        {
            return Tag + " joining the match lobby id=" + Token(lobbyId) + " for voice";
        }

        public static string Joined(string lobbyId)
        {
            return Tag + " joined the match lobby id=" + Token(lobbyId);
        }

        public static string NotJoined(string lobbyId, string reason)
        {
            return Tag + " cannot join the match lobby id=" + Token(lobbyId) + " reason=" + Token(reason) + "; playing without voice";
        }

        public static string NoVoiceRoom(string lobbyId)
        {
            return Tag + " the match lobby id=" + Token(lobbyId) + " has no voice room; playing without voice";
        }

        public static string TimedOut(float seconds)
        {
            return "no answer from the lobby service after " + seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        public static string Left(string lobbyId)
        {
            return Tag + " left the match lobby id=" + Token(lobbyId);
        }

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
