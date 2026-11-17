using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class PartyLog
    {
        public const string Tag = "[Party]";

        public static string Off(string reason)
        {
            return Tag + " party and join code are off: " + reason;
        }

        public static string On(string version)
        {
            return Tag + " party and join code are on version=" + version;
        }

        public static string LoggingIn()
        {
            return Tag + " logging in";
        }

        public static string LoggedIn(string userId)
        {
            return Tag + " logged in user=" + userId;
        }

        public static string Creating(string bucketId, int maxMembers)
        {
            return Tag + " creating bucket=" + bucketId + " max=" + maxMembers.ToString(CultureInfo.InvariantCulture);
        }

        public static string Joining(string partyId)
        {
            return Tag + " joining id=" + partyId;
        }

        public static string Joined(LobbyInfo lobby, string leaderUserId)
        {
            return Tag + " joined id=" + lobby.Id + " leader=" + leaderUserId + " members=" + lobby.DescribeMembers();
        }

        public static string MembersChanged(LobbyInfo lobby)
        {
            return Tag + " members id=" + lobby.Id + " " + lobby.DescribeMembers();
        }

        public static string LeaderChanged(string partyId, string previousLeader, string leader)
        {
            return Tag + " leader id=" + partyId + " " + (previousLeader ?? "-") + " -> " + (leader ?? "-");
        }

        public static string LeaderState(string partyId, PartyState state, string endpoint, long match)
        {
            return Tag + " leader state id=" + partyId + " state=" + PartyLobby.StateText(state)
                + (state == PartyState.InMatch
                    ? " server=" + endpoint + " match=" + match.ToString(CultureInfo.InvariantCulture)
                    : "");
        }

        public static string Following(string partyId, string leaderUserId, string endpoint, long match)
        {
            return Tag + " following id=" + partyId + " leader=" + leaderUserId + " server=" + endpoint
                + " match=" + match.ToString(CultureInfo.InvariantCulture);
        }

        public static string UnreadableLeader(string partyId, string leaderUserId)
        {
            return Tag + " ignoring unreadable state of leader=" + leaderUserId + " id=" + partyId;
        }

        public static string Leaving(string partyId)
        {
            return Tag + " leaving id=" + partyId;
        }

        public static string Left(string partyId, string error)
        {
            return Tag + " left id=" + partyId + (error == null ? "" : " (leave reported: " + error + ")");
        }

        public static string Retrying(string what, string partyId, string error)
        {
            return Tag + " " + what + " failed id=" + partyId + ": " + error + "; retrying";
        }

        public static string MemberSkill(string partyId, string userId, int? skillRating)
        {
            return Tag + " skill id=" + partyId + " member=" + userId + " rating="
                + (skillRating.HasValue ? skillRating.Value.ToString(CultureInfo.InvariantCulture) : "-");
        }

        public static string SkillsUnavailable(string partyId, string what)
        {
            return Tag + " " + what + " is not supported by the lobby service id=" + partyId
                + "; skill ratings are no longer shared in this session";
        }

        public static string Failed(PartyError error, string reason)
        {
            return Tag + " " + error + ": " + reason;
        }
    }
}
