using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class PartyLobby
    {
        public const string VersionKey = "version";

        public const string StateKey = "party_state";

        public const string ServerKey = "party_server";

        // サーバーのアドレスだけでなく番号も載せるのは、同じサーバーを続けて選んでもメンバーが新しい決定と見分けられるようにするため。
        public const string MatchKey = "party_match";

        // リーダーの状態と違い、どのメンバーも自分の属性に載せる。リーダーが全員分を読んで Quick Match に平均を渡すため。
        public const string SkillKey = "party_skill";

        public const string IdleState = "idle";

        public const string SearchingState = "searching";

        public const string InMatchState = "match";

        // サーバーのロビー (ServerLobby.BucketPrefix) と混ざらないよう、別の接頭辞にする。
        public const string BucketPrefix = "shooter-party:";

        public const int DefaultMaxMembers = 4;

        // 1 人ではパーティにならないので、下限は 2 にする。
        public const int MinMaxMembers = 2;

        // EOS のロビーの最大人数 64 を超えない値にする。
        public const int MaxMaxMembers = 16;

        public const float DefaultPollIntervalSeconds = 1f;

        public static string BucketId(string version)
        {
            return BucketPrefix + LobbyLaunchOptions.BucketVersion(version);
        }

        public static bool IsValidMaxMembers(int maxMembers)
        {
            return maxMembers >= MinMaxMembers && maxMembers <= MaxMaxMembers;
        }

        public static IReadOnlyList<LobbyAttribute> LobbyAttributes(string version)
        {
            return new[] { LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)) };
        }

        // 版の属性が無いロビーを同じ版とみなすのは、属性を返さないサービスのロビーも弾かないようにするため。
        public static bool IsSameVersion(LobbyInfo lobby, string version)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return !lobby.TryGetText(VersionKey, out var lobbyVersion)
                || string.Equals(lobbyVersion, LobbyLaunchOptions.BucketVersion(version), StringComparison.Ordinal);
        }

        // オーナーが抜けてまだ渡されていなければ残っている最初のメンバーにするのは、どのメンバーが読んでも同じ人になるようにするため。
        public static string Leader(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            if (lobby.HasMember(lobby.OwnerUserId))
            {
                return lobby.OwnerUserId;
            }

            return lobby.MemberCount > 0 ? lobby.Members[0] : null;
        }

        public static IReadOnlyList<LobbyAttribute> LeaderAttributes(PartyState state, string endpoint, long match)
        {
            if (state != PartyState.InMatch)
            {
                return new[] { LobbyAttribute.OfText(StateKey, StateText(state)) };
            }

            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out _, out _))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(endpoint));
            }

            return new[]
            {
                LobbyAttribute.OfText(StateKey, InMatchState),
                LobbyAttribute.OfText(ServerKey, endpoint),
                LobbyAttribute.OfNumber(MatchKey, match),
            };
        }

        public static bool TryReadLeader(
            IReadOnlyList<LobbyAttribute> attributes, out PartyState state, out string endpoint, out long match)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            state = PartyState.Idle;
            endpoint = null;
            match = 0;
            var stateAttribute = Find(attributes, StateKey);
            if (stateAttribute == null)
            {
                return true;
            }

            if (!stateAttribute.IsText || !TryParseState(stateAttribute.Text, out var parsed))
            {
                return false;
            }

            if (parsed != PartyState.InMatch)
            {
                state = parsed;
                return true;
            }

            var server = Find(attributes, ServerKey);
            var number = Find(attributes, MatchKey);
            if (server == null || !server.IsText || !NetLaunchOptions.TryParseEndpoint(server.Text, out _, out _)
                || number == null || number.IsText)
            {
                return false;
            }

            state = PartyState.InMatch;
            endpoint = server.Text;
            match = number.Number;
            return true;
        }

        public static IReadOnlyList<LobbyAttribute> SkillAttributes(int skillRating)
        {
            return new[] { LobbyAttribute.OfNumber(SkillKey, skillRating) };
        }

        // 値が無い・読めないメンバーを 0 でなく null にするのは、ServerLobby.SkillAverage がその人を数えないようにするため。
        public static int? ReadSkill(IReadOnlyList<LobbyAttribute> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            var skill = Find(attributes, SkillKey);
            if (skill == null || skill.IsText || skill.Number < int.MinValue || skill.Number > int.MaxValue)
            {
                return null;
            }

            return (int)skill.Number;
        }

        public static NetLaunchOptions ConnectOptions(string endpoint)
        {
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(endpoint));
            }

            return new NetLaunchOptions(NetLaunchMode.Client, address, port, NetTransportKind.Udp, null);
        }

        public static string StateText(PartyState state)
        {
            switch (state)
            {
                case PartyState.Idle:
                    return IdleState;
                case PartyState.Searching:
                    return SearchingState;
                case PartyState.InMatch:
                    return InMatchState;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "unknown party state");
            }
        }

        public static bool TryParseState(string text, out PartyState state)
        {
            switch (text)
            {
                case IdleState:
                    state = PartyState.Idle;
                    return true;
                case SearchingState:
                    state = PartyState.Searching;
                    return true;
                case InMatchState:
                    state = PartyState.InMatch;
                    return true;
                default:
                    state = PartyState.Idle;
                    return false;
            }
        }

        private static LobbyAttribute Find(IReadOnlyList<LobbyAttribute> attributes, string key)
        {
            foreach (var attribute in attributes)
            {
                if (attribute != null && string.Equals(attribute.Key, key, StringComparison.Ordinal))
                {
                    return attribute;
                }
            }

            return null;
        }
    }
}
