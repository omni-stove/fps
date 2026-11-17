using System.Globalization;

namespace Shooter.NetV2
{
    public static class MemberLobbyLog
    {
        public const string Tag = "[Lobby]";

        public static string Created(string lobbyId, string bucketId, int members, int maxMembers)
        {
            return Tag + " member lobby created id=" + Token(lobbyId) + " bucket=" + Token(bucketId)
                + " members=" + Count(members) + "/" + Count(maxMembers);
        }

        public static string CreateFailed(string bucketId, string reason)
        {
            return Tag + " member lobby create failed bucket=" + Token(bucketId) + " reason=" + Token(reason);
        }

        public static string Joined(string lobbyId, int members, int maxMembers)
        {
            return Tag + " member lobby joined id=" + Token(lobbyId) + " members=" + Count(members) + "/" + Count(maxMembers);
        }

        public static string JoinFailed(string lobbyId, string reason)
        {
            return Tag + " member lobby join failed id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        public static string Abandoned(string lobbyId, string reason)
        {
            return Tag + " member lobby abandoned id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        public static string Left(string lobbyId)
        {
            return Tag + " member lobby left id=" + Token(lobbyId);
        }

        public static string LeaveFailed(string lobbyId, string reason)
        {
            return Tag + " member lobby leave failed id=" + Token(lobbyId) + " reason=" + Token(reason);
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Token(string text) => string.IsNullOrEmpty(text) ? "none" : text;
    }
}
