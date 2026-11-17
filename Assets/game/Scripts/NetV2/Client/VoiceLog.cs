using System.Globalization;

namespace Shooter.NetV2
{
    /// <summary>試合のボイスチャット (<see cref="IVoiceSession"/>) の <c>[Voice]</c> 付きログの文言。</summary>
    public static class VoiceLog
    {
        public const string Tag = "[Voice]";

        /// <example><c>[Voice] joining lobby=abc123</c></example>
        public static string Joining(string lobbyId)
        {
            return Tag + " joining lobby=" + Token(lobbyId);
        }

        /// <example><c>[Voice] join failed lobby=abc123 reason=not logged in to EOS</c></example>
        public static string JoinFailed(string lobbyId, string reason)
        {
            return Tag + " join failed lobby=" + Token(lobbyId) + " reason=" + reason;
        }

        /// <example><c>[Voice] connected lobby=abc123</c></example>
        public static string Connected(string lobbyId)
        {
            return Tag + " connected lobby=" + Token(lobbyId);
        }

        /// <example><c>[Voice] connection lost lobby=abc123 reason=NoConnection</c></example>
        public static string ConnectionLost(string lobbyId, string reason)
        {
            return Tag + " connection lost lobby=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        /// <example><c>[Voice] left lobby=abc123</c></example>
        public static string Left(string lobbyId)
        {
            return Tag + " left lobby=" + Token(lobbyId);
        }

        /// <example><c>[Voice] sending=true failed reason=TooManyRequests</c></example>
        public static string SendingFailed(bool enabled, string reason)
        {
            return Tag + " sending=" + Flag(enabled) + " failed reason=" + reason;
        }

        /// <example><c>[Voice] receiving=false participant=0002aa failed reason=NotFound</c></example>
        public static string ReceivingFailed(string participantId, bool enabled, string reason)
        {
            return Tag + " receiving=" + Flag(enabled) + " participant=" + Token(participantId) + " failed reason=" + reason;
        }

        /// <example><c>[Voice] volume=0.50 failed reason=InvalidParameters</c></example>
        public static string VolumeFailed(float volume, string reason)
        {
            return Tag + " volume=" + volume.ToString("0.00", CultureInfo.InvariantCulture) + " failed reason=" + reason;
        }

        /// <summary>部屋から出る要求をサービスが断った。こちらはもう部屋にいないものとして進める。</summary>
        /// <example><c>[Voice] leave failed lobby=abc123 reason=NotFound</c></example>
        public static string LeaveFailed(string lobbyId, string reason)
        {
            return Tag + " leave failed lobby=" + Token(lobbyId) + " reason=" + reason;
        }

        /// <summary>この試合ではボイスを使わない (試合のロビーが分からない・ボイスの部屋が作れない)。</summary>
        /// <example><c>[Voice] off reason=the match has no lobby</c></example>
        public static string Off(string reason)
        {
            return Tag + " off reason=" + reason;
        }

        /// <summary>押して話すアクションが入力の資産に無いので、押して話せない。</summary>
        /// <example><c>[Voice] input action Player/PushToTalk is missing; push to talk does nothing</c></example>
        public static string PushToTalkMissing(string actionPath)
        {
            return Tag + " input action " + actionPath + " is missing; push to talk does nothing";
        }

        private static string Flag(bool value) => value ? "true" : "false";

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
