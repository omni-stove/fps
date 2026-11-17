using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// パーティ (EOS のメンバー用ロビー) の決まりごと。リーダーは入るサーバーと状態を自分のメンバーの属性に書き、
    /// メンバーはリーダーの属性を読んで同じサーバーに入る。招待のコードはロビーの Id そのもの。
    /// </summary>
    public static class PartyLobby
    {
        /// <summary>パーティのロビーに付ける、ビルドの版の属性 (文字列)。版の違うビルドとは同じパーティに入らない。</summary>
        public const string VersionKey = "version";

        /// <summary>リーダーの今の状態 (文字列。<see cref="IdleState"/> / <see cref="SearchingState"/> / <see cref="InMatchState"/>)。</summary>
        public const string StateKey = "party_state";

        /// <summary>リーダーが入るサーバーの公開アドレス (<c>address:port</c>)。状態が <see cref="InMatchState"/> のときだけ読む。</summary>
        public const string ServerKey = "party_server";

        /// <summary>
        /// リーダーがサーバーを決めるたびに 1 つ増やす番号 (整数)。同じサーバーを続けて選んでも、メンバーが新しい決定と見分けられる。
        /// </summary>
        public const string MatchKey = "party_match";

        public const string IdleState = "idle";

        public const string SearchingState = "searching";

        public const string InMatchState = "match";

        /// <summary>パーティのロビーのバケット ID の接頭辞。サーバーのロビー (<see cref="ServerLobby.BucketPrefix"/>) と混ざらないようにする。</summary>
        public const string BucketPrefix = "shooter-party:";

        /// <summary>パーティに入れる人数の既定。ゲームはセッションを作るときに別の値を渡せる。</summary>
        public const int DefaultMaxMembers = 4;

        /// <summary>パーティに入れる人数の下限。1 人ではパーティにならない。</summary>
        public const int MinMaxMembers = 2;

        /// <summary>パーティに入れる人数の上限 (EOS のロビーの最大人数 64 以下)。</summary>
        public const int MaxMaxMembers = 16;

        /// <summary>メンバーとリーダーの属性を読み直す間隔の既定 (秒)。</summary>
        public const float DefaultPollIntervalSeconds = 1f;

        /// <summary>パーティのロビーのバケット ID。</summary>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        public static string BucketId(string version)
        {
            return BucketPrefix + LobbyLaunchOptions.BucketVersion(version);
        }

        /// <summary><paramref name="maxMembers"/> が <see cref="MinMaxMembers"/> 〜 <see cref="MaxMaxMembers"/> に入るか。</summary>
        public static bool IsValidMaxMembers(int maxMembers)
        {
            return maxMembers >= MinMaxMembers && maxMembers <= MaxMaxMembers;
        }

        /// <summary>パーティのロビーを作るときに付ける属性 (version)。</summary>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        public static IReadOnlyList<LobbyAttribute> LobbyAttributes(string version)
        {
            return new[] { LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)) };
        }

        /// <summary>
        /// lobby が自分の版のパーティか。版の属性が無いロビー (属性を返さないサービス) は同じ版とみなす。
        /// </summary>
        public static bool IsSameVersion(LobbyInfo lobby, string version)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return !lobby.TryGetText(VersionKey, out var lobbyVersion)
                || string.Equals(lobbyVersion, LobbyLaunchOptions.BucketVersion(version), StringComparison.Ordinal);
        }

        /// <summary>
        /// パーティのリーダー。オーナーがまだメンバーならオーナー、オーナーが抜けたのにまだ渡されていなければ、残っている最初のメンバー。
        /// 誰もいなければ null。どのメンバーが読んでも同じ人になる。
        /// </summary>
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

        /// <summary>リーダーが自分のメンバーの属性に書く状態。<see cref="PartyState.InMatch"/> のときだけサーバーと番号を書く。</summary>
        /// <param name="state">今の状態。</param>
        /// <param name="endpoint">入るサーバーの公開アドレス (<c>address:port</c>)。<see cref="PartyState.InMatch"/> のときだけ使う。</param>
        /// <param name="match">サーバーを決めた番号。<see cref="PartyState.InMatch"/> のときだけ使う。</param>
        /// <exception cref="ArgumentException"><see cref="PartyState.InMatch"/> なのにアドレスが IPv4:port でない。</exception>
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

        /// <summary>
        /// リーダーのメンバーの属性から状態を読む。状態の属性が無ければ <see cref="PartyState.Idle"/>。
        /// 知らない状態、または <see cref="PartyState.InMatch"/> なのにアドレスか番号が無い・読めないときは false を返し、
        /// 状態を <see cref="PartyState.Idle"/> にする。
        /// </summary>
        /// <param name="attributes">リーダーのメンバーの属性。</param>
        /// <param name="state">読んだ状態。</param>
        /// <param name="endpoint"><see cref="PartyState.InMatch"/> のときのサーバーの公開アドレス。ほかは null。</param>
        /// <param name="match"><see cref="PartyState.InMatch"/> のときのサーバーを決めた番号。ほかは 0。</param>
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

        /// <summary>リーダーが決めたサーバーの公開アドレスへ UDP でつなぐクライアントの起動設定。</summary>
        /// <exception cref="ArgumentException">アドレスが IPv4:port でない。</exception>
        public static NetLaunchOptions ConnectOptions(string endpoint)
        {
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(endpoint));
            }

            return new NetLaunchOptions(NetLaunchMode.Client, address, port, NetTransportKind.Udp, null);
        }

        /// <summary>状態の属性に書く文字列。</summary>
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

        /// <summary>状態の属性の文字列を読む。知らない文字列なら false。</summary>
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
