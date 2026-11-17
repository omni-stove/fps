using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class ServerLobby
    {
        public const string ServerKey = "server";

        public const string ModeKey = "mode";

        public const string VersionKey = "version";

        public const string PlayersKey = "players";

        public const string PrivateKey = "private";

        public const string JoinCodeKey = "join_code";

        public const string SkillAverageKey = "skill_average";

        public const string VoiceEnabledKey = "voice_enabled";

        public const string TeamDeathmatchMode = "tdm";

        // 同じ EOS のサンドボックスを使う別のゲームのロビーと混ざらないよう、接頭辞を付ける。
        public const string BucketPrefix = "shooter-server:";

        // プレイヤーと見分けるため、サーバーのユーザーの表示名は分ける。
        public const string DisplayName = "shooter-server";

        public const float RetryIntervalSeconds = 5f;

        public const float TimeoutSeconds = 60f;

        // 版の違うビルド同士が同じバケットに入らないよう、版を含める。
        public static string BucketId(string version)
        {
            return BucketPrefix + LobbyLaunchOptions.BucketVersion(version);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players)
        {
            return Attributes(endpoint, version, players, NetMatchSettings.Default);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players, NetMatchSettings match)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("must not be null or blank", nameof(endpoint));
            }

            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var attributes = new List<LobbyAttribute>
            {
                LobbyAttribute.OfText(ServerKey, endpoint),
                LobbyAttribute.OfText(ModeKey, TeamDeathmatchMode),
                LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)),
                PlayersAttribute(players),
                // サーバーのロビーはいつも RTC 部屋を開いて作るので 1。
                LobbyAttribute.OfNumber(VoiceEnabledKey, 1),
            };

            if (match.PrivateMatch)
            {
                // コードが無くても非公開の印は付け、誰も入れないサーバーを Quick Match が選ばないようにする
                attributes.Add(LobbyAttribute.OfNumber(PrivateKey, 1));
                if (JoinCode.TryNormalize(match.JoinCode, out var code))
                {
                    attributes.Add(LobbyAttribute.OfText(JoinCodeKey, code));
                }
            }

            return attributes;
        }

        // ボイスのためにプレイヤーもメンバーとして入り、オーナーのサーバー自身も 1 人分を使うので 1 足す。
        public static int MaxMembers(int maxPlayers)
        {
            if (maxPlayers < NetMatchSettings.MinMaxPlayers || maxPlayers > NetMatchSettings.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + NetMatchSettings.MinMaxPlayers + " and " + NetMatchSettings.MaxMaxPlayers);
            }

            return Math.Min(maxPlayers + 1, LobbyLaunchOptions.MaxMaxPlayers);
        }

        // ボイスのあるロビーではサーバー自身もメンバーとして 1 人分を使うので、その分を除く。
        public static int PlayerSlots(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return lobby.VoiceEnabled ? lobby.MaxMembers - 1 : lobby.MaxMembers;
        }

        public static LobbyAttribute PlayersAttribute(int players)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            return LobbyAttribute.OfNumber(PlayersKey, players);
        }

        // サーバーは UDP で待ち受けるときだけロビーに載る (-register-lobby は -transport udp だけ) ので、UDP でつなぐ。
        public static NetLaunchOptions ConnectOptions(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            lobby.TryGetText(ServerKey, out var endpoint);
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(lobby));
            }

            return new NetLaunchOptions(NetLaunchMode.Client, address, port, NetTransportKind.Udp, null);
        }
    }
}
