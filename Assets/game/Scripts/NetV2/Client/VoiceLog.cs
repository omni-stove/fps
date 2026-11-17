using System.Globalization;

namespace Shooter.NetV2
{
    public static class VoiceLog
    {
        public const string Tag = "[Voice]";

        public static string Joining(string lobbyId)
        {
            return Tag + " joining lobby=" + Token(lobbyId);
        }

        public static string JoinFailed(string lobbyId, string reason)
        {
            return Tag + " join failed lobby=" + Token(lobbyId) + " reason=" + reason;
        }

        public static string Connected(string lobbyId)
        {
            return Tag + " connected lobby=" + Token(lobbyId);
        }

        public static string ConnectionLost(string lobbyId, string reason)
        {
            return Tag + " connection lost lobby=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        public static string Left(string lobbyId)
        {
            return Tag + " left lobby=" + Token(lobbyId);
        }

        public static string SendingFailed(bool enabled, string reason)
        {
            return Tag + " sending=" + Flag(enabled) + " failed reason=" + reason;
        }

        public static string ReceivingFailed(string participantId, bool enabled, string reason)
        {
            return Tag + " receiving=" + Flag(enabled) + " participant=" + Token(participantId) + " failed reason=" + reason;
        }

        public static string VolumeFailed(float volume, string reason)
        {
            return Tag + " volume=" + volume.ToString("0.00", CultureInfo.InvariantCulture) + " failed reason=" + reason;
        }

        public static string LeaveFailed(string lobbyId, string reason)
        {
            return Tag + " leave failed lobby=" + Token(lobbyId) + " reason=" + reason;
        }

        public static string Off(string reason)
        {
            return Tag + " off reason=" + reason;
        }

        public static string PushToTalkMissing(string actionPath)
        {
            return Tag + " input action " + actionPath + " is missing; push to talk does nothing";
        }

        private static string Flag(bool value) => value ? "true" : "false";

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
