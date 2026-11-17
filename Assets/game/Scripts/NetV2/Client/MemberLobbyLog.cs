using System.Globalization;

namespace Shooter.NetV2
{
    /// <summary>メンバー用ロビー (<see cref="MemberLobbies"/>) の <c>[Lobby]</c> 付きログの文言。</summary>
    public static class MemberLobbyLog
    {
        public const string Tag = "[Lobby]";

        /// <example><c>[Lobby] member lobby created id=abc123 bucket=shooter-party:0.3.0 members=1/4</c></example>
        public static string Created(string lobbyId, string bucketId, int members, int maxMembers)
        {
            return Tag + " member lobby created id=" + Token(lobbyId) + " bucket=" + Token(bucketId)
                + " members=" + Count(members) + "/" + Count(maxMembers);
        }

        /// <example><c>[Lobby] member lobby create failed bucket=shooter-party:0.3.0 reason=TooManyRequests</c></example>
        public static string CreateFailed(string bucketId, string reason)
        {
            return Tag + " member lobby create failed bucket=" + Token(bucketId) + " reason=" + Token(reason);
        }

        /// <example><c>[Lobby] member lobby joined id=abc123 members=2/4</c></example>
        public static string Joined(string lobbyId, int members, int maxMembers)
        {
            return Tag + " member lobby joined id=" + Token(lobbyId) + " members=" + Count(members) + "/" + Count(maxMembers);
        }

        /// <example><c>[Lobby] member lobby join failed id=abc123 reason=not-found</c></example>
        public static string JoinFailed(string lobbyId, string reason)
        {
            return Tag + " member lobby join failed id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        /// <summary>作った・入ったロビーを使える形にできなかったので、出ていく。</summary>
        /// <example><c>[Lobby] member lobby abandoned id=abc123 reason=InvalidParameters</c></example>
        public static string Abandoned(string lobbyId, string reason)
        {
            return Tag + " member lobby abandoned id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        /// <example><c>[Lobby] member lobby left id=abc123</c></example>
        public static string Left(string lobbyId)
        {
            return Tag + " member lobby left id=" + Token(lobbyId);
        }

        /// <example><c>[Lobby] member lobby leave failed id=abc123 reason=TimedOut</c></example>
        public static string LeaveFailed(string lobbyId, string reason)
        {
            return Tag + " member lobby leave failed id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
